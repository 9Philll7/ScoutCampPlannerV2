using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;

namespace ScoutCampPlanner.MealPlanningSeed;

internal static class AcceptanceSeed
{
    internal static Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("TEST meal acceptance v1/" + key)).AsSpan(0, 16));
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2027, 7, 1);
    private const string Source = "TEST synthetische Abnahmedaten; keine Lebensmittel- oder medizinischen Empfehlungen";

    internal static async Task CreateAsync(PlatformDbContext platform, CampDbContext camp, CateringDbContext catering)
    {
        // Do not activate misleading fixtures if the production snapshot codec drops conflict IDs.
        var probe = new RecipeSnapshot(2, "TEST snapshot codec", null, null, null, RecipeType.PortionBased,
            new(1, 1, null, null), null, false, [], [], [], [], [new(ConflictType.Allergen, Id("codec-probe"))]);
        var readProbe = RecipeSnapshotBuilder.Deserialize(RecipeSnapshotBuilder.Serialize(probe));
        if (readProbe.ExposedConflicts.Single() != probe.ExposedConflicts.Single())
            throw new InvalidOperationException("Existing recipe snapshot codec loses ConflictReference IDs. Seed activation blocked; production-path correction requires separate approval.");
        Guid tenant = Id("tenant"), campId = Id("camp"), actor = Id("actor"), membership = Id("membership"), campMember = Id("camp-member");
        var user = new UserAccount(actor, "acceptance@example.test");
        user.ActivateAfterInitialSetup();
        using var passwords = new Argon2idPasswordVerifier(Argon2idOperatingMode.Server);
        platform.AddRange(new Tenant(tenant, "TEST Mahlzeitenplanung Inkrement 2"), user,
            new PasswordCredential(actor, await passwords.CreateAsync("TEST-only-MealPlanning-2026!"), Timestamp),
            new TenantMembership(membership, actor, tenant), new TenantRoleAssignment(membership, Roles.TenantOwner),
            new CampMembership(campMember, membership, campId), new CampRoleAssignment(campMember, Roles.CampAdmin),
            new PlatformRoleAssignment(actor, Roles.PlatformAdmin));
        foreach (string permission in new[] { Permissions.Health.ReadParticipantRequirements, Permissions.Health.EditParticipantRequirements, Permissions.Catering.VerifyMealPlanning })
            platform.Add(new CampPermissionGrant(campMember, permission));
        await platform.SaveChangesAsync();
        camp.Add(new Camp.Domain.Camp(campId, tenant, "TEST Versorgung - nur Dummy-Daten", Start, Start.AddDays(5)));
        camp.Add(new CampStage(Id("stage"), campId, "TEST Stufe", 0));
        camp.Add(new StructureNode(Id("root"), campId, null, "TEST Gruppen"));
        for (int i = 0; i < 2; i++) camp.Add(new StructureNode(Id("node" + i), campId, Id("root"), "TEST Gruppe " + (i == 0 ? "A All" : "B komplementaer")));
        await camp.SaveChangesAsync();
        var references = await new IngredientEditorReferenceDataStore(catering).GetAsync();
        Guid gram = references.Units.Single(x => x.Symbol == "g").Id;
        Guid milk = references.Allergens.Single(x => x.Code == "MILK").Id;
        // Disjoint but identical populations make the preference comparison unambiguous.
        for (int comparison = 0; comparison < 3; comparison++)
        {
            Guid node = Id("comparison-node" + comparison);
            camp.Add(new StructureNode(node, campId, Id("root"), "TEST Vergleich " + (CookingTogetherPreference)comparison));
            for (int personIndex = 0; personIndex < 4; personIndex++)
            {
                var person = new Participant(Id($"comparison-person{comparison}-{personIndex}"), campId,
                    $"TEST Vergleich {comparison + 1}/{personIndex + 1} " + (personIndex < 2 ? "Standard" : "Milchallergie"));
                person.AssignStructureNode(node);
                person.SetRequirements(null, personIndex < 2 ? [] : [milk], []);
                camp.Add(person);
            }
        }
        Guid lactose = references.Intolerances.Single(x => x.Code == "LACTOSE").Id;
        Guid fructose = references.Intolerances.Single(x => x.Code == "FRUCTOSE").Id;
        Guid plant = references.Origins.Single(x => x.Code == "PLANT").Id;
        Guid dairy = references.Origins.Single(x => x.Code == "DAIRY").Id;
        // Existing replacement-rule persistence still references the legacy conflict catalogue.
        // Mirror its catalogue ID in this isolated fixture, not a new business definition.
        if (!await catering.Allergens.AnyAsync(x => x.Id == milk)) catering.Add(new Allergen(milk, "TEST Milch"));
        await catering.SaveChangesAsync();
        var dietStore = new DietaryCatalogStore(catering);
        for (int i = 0; i < 2; i++)
        {
            var rules = references.Origins.Where(x => DietaryCatalogStore.MainOriginCodes.Contains(x.Code))
                .Select(x => new DietaryOriginRuleData(x.Id, x.Id == plant || i == 1 && x.Id == dairy ? DietaryOriginDecision.Allowed : DietaryOriginDecision.Excluded)).ToArray();
            var result = await dietStore.SaveAsync(Id("diet" + i), i == 0 ? null : tenant,
                new(i == 0 ? "TEST nur pflanzlich zentral" : "TEST Pflanze und Milch Mandant", Source, i, 0, rules), default);
            if (result.Status != DietaryMutationStatus.Success) throw new InvalidOperationException("Diet seed: " + result);
        }
        string[] mealNames = ["TEST Fruehstueck", "TEST Mittagessen", "TEST Abendessen"];
        var meals = new List<CampMeal>();
        for (int type = 0; type < 3; type++)
        {
            catering.Add(new CampMealType(Id("type" + type), campId, mealNames[type], type));
            for (int day = 0; day < 6; day++)
            {
                var meal = new CampMeal(Id($"meal{day}-{type}"), campId, Id("type" + type), Start.AddDays(day));
                meals.Add(meal); catering.Add(meal);
            }
        }
        for (int i = 0; i < 20; i++)
        {
            int profile = i % 10;
            string label = profile switch { 0 or 1 => "Standard", 2 or 3 => "Milchallergie", 4 => "Pflanzlich", 5 => "Pflanze-Milch", 6 or 7 => "Laktose 0.5g TEST", 8 => "Laktose 2g TEST", _ => "Fruktose ohne Schwelle" };
            var person = new Participant(Id("person" + i), campId, $"TEST {i + 1:D2} {label}");
            person.AssignStructureNode(Id("node" + i / 10));
            person.SetRequirements(profile is 4 or 5 ? Id("diet" + (profile - 4)) : null,
                profile is 2 or 3 ? [milk] : [],
                profile is 6 or 7 or 8 ? [new(lactose, profile == 8 ? 2m : 0.5m, Source)] : profile == 9 ? [new(fructose, null, Source)] : []);
            if (i is 1 or 11) person.SetDayAbsent(Start.AddDays(1), true);
            if (i is 3 or 13) person.SetMealAbsent(Id("meal2-0"), true);
            camp.Add(person);
        }
        await camp.SaveChangesAsync(); await catering.SaveChangesAsync();

        var ingredientStore = new IngredientRevisionWorkflowStore(catering);
        async Task<Guid> Ingredient(string key, string origin, IngredientPropertyState allergen, decimal? amount = null,
            IngredientPropertyState qualitative = IngredientPropertyState.DoesNotContain, bool variants = false, bool unknown = false)
        {
            var content = IngredientRevisionDraftContent.Create("TEST " + key, references.Categories.Single(x => x.Code == "OTHER").Id, gram,
                IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed,
                allergens: references.Allergens.Select(x => new IngredientPropertyValue(x.Id, x.Id == milk ? allergen : IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)),
                intolerances: references.Intolerances.Where(x => !amount.HasValue || x.Id != lactose).Select(x => new IngredientPropertyValue(x.Id, unknown ? IngredientPropertyState.Unknown : x.Id == lactose ? qualitative : IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)),
                origins: [new(references.Origins.Single(x => x.Code == origin).Id, IngredientPropertyState.Contains, IngredientPropertySource.ManuallyVerified)],
                variants: variants ? [new(Id(key + "free"), "free", "TEST ohne Milchallergen", true, 0,
                    allergenOverrides: [new(milk, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)]),
                    new(Id(key + "inherit"), "inherit", "TEST wie Basis", true, 1)] : [],
                substanceContents: amount.HasValue ? [new(lactose, amount.Value, gram, 100, gram, IngredientSubstanceContentSourceType.ManualEstimate, Source, IngredientSubstanceContentReviewState.Reviewed)] : [],
                sourceSummary: Source);
            Guid revision = Id("ingredient-revision/" + key);
            var created = await ingredientStore.CreateDraftAsync(Id("ingredient/" + key), revision, new(IngredientScopeType.Camp, campId), content, actor, Timestamp);
            if (created.Status != IngredientRevisionMutationStatus.Created) throw new InvalidOperationException(key + ": " + created);
            var saved = await ingredientStore.SaveDraftAsync(revision, content, created.RowVersion!.Value, actor, Timestamp);
            if (saved.Status != IngredientRevisionMutationStatus.Saved) throw new InvalidOperationException(key + ": " + saved);
            var published = await ingredientStore.PublishAsync(revision, saved.RowVersion!.Value, actor, Timestamp);
            if (published.Status != IngredientRevisionMutationStatus.Published) throw new InvalidOperationException(key + ": " + published);
            return revision;
        }
        Guid good = await Ingredient("Pflanze ohne Konflikte", "PLANT", IngredientPropertyState.DoesNotContain);
        Guid good2 = await Ingredient("Pflanze Ersatz B", "PLANT", IngredientPropertyState.DoesNotContain);
        Guid bad = await Ingredient("Milchallergen ohne Variante", "DAIRY", IngredientPropertyState.Contains);
        Guid variant = await Ingredient("Milchallergen mit Varianten", "DAIRY", IngredientPropertyState.Contains, variants: true);
        Guid quantitative = await Ingredient("Quantitativ Laktose 10g pro 100g", "PLANT", IngredientPropertyState.DoesNotContain, amount: 10);
        Guid qualitative = await Ingredient("Qualitativ Laktose enthalten", "PLANT", IngredientPropertyState.DoesNotContain, qualitative: IngredientPropertyState.Contains);
        Guid unknown = await Ingredient("Unknown Stoffdaten", "PLANT", IngredientPropertyState.DoesNotContain, unknown: true);
        await Ingredient("Fisch Herkunft", "FISH", IngredientPropertyState.DoesNotContain);

        var drafts = new RecipeDraftStore(catering);
        var recipeReferences = new EfRecipeReferences(catering);
        var publisher = new RecipePublisher(catering, drafts, new RecipePublicationValidator(recipeReferences), new RecipeSnapshotBuilder(recipeReferences));
        var recipes = new List<(string Name, Guid Revision)>();
        async Task<Guid> Recipe(string name, Action<RecipeDraft> configure)
        {
            var draft = new RecipeDraft(Id("recipe/" + name), RecipeScopeType.Camp, campId, RecipeType.PortionBased, "TEST " + name);
            draft.ConfigurePortionReference(1, false); draft.SetDetails(Source, Source, "TEST"); configure(draft);
            await new RecipeEditorStore(catering, drafts).CreateCampAsync(draft, campId, actor, Timestamp);
            var publication = await publisher.PublishAsync(draft.Id, 0, actor, Timestamp, true);
            if (publication.Revision is null) throw new InvalidOperationException(name + ": " + JsonSerializer.Serialize(publication));
            string publishedJson = publication.Revision.SnapshotJson;
            if (RecipeSnapshotBuilder.Serialize(RecipeSnapshotBuilder.Deserialize(publishedJson)) != publishedJson)
                throw new InvalidOperationException(name + ": published snapshot codec roundtrip failed.");
            recipes.Add((name, publication.Revision.Id));
            return publication.Revision.Id;
        }
        void Position(RecipeDraft d, Guid ingredient, Guid[]? replacements = null, ScalingMode mode = ScalingMode.Linear)
        {
            int n = d.IngredientPositions.Count;
            // Repeated ingredients use distinct groups so draft publication remains valid.
            Guid group = Id(d.Name + "/group" + n); d.AddGroup(new(group, d.Id, "TEST Position " + (n + 1), n));
            var p = new RecipeIngredientPosition(Id(d.Name + "/position" + n), d.Id, group, ingredient, 10, gram, n, mode,
                AgeGroupScalingMode.Ignore, mode == ScalingMode.Stepwise ? new(3, 30) : null);
            foreach (Guid replacement in replacements ?? []) p.AddReplacementRule(new(Id(p.Id + "/" + replacement), p.Id, replacement, 10, gram, [new(ConflictType.Allergen, milk)]));
            d.AddIngredientPosition(p);
        }
        void Child(RecipeDraft d, Guid revision, Guid? replacement = null)
        {
            int n = d.SubrecipePositions.Count;
            Guid group = Id(d.Name + "/child-group" + n);
            d.AddGroup(new(group, d.Id, "TEST Teilrezeptvorkommen " + (n + 1), n));
            var p = new RecipeSubrecipePosition(Id(d.Name + "/child" + n), d.Id, group, revision, 1, null, null, n);
            if (replacement.HasValue) p.AddReplacementRule(new(Id(p.Id + "/replacement"), p.Id, replacement, 1, null, null, [new(ConflictType.Allergen, milk)]));
            d.AddSubrecipePosition(p);
        }
        Guid standard = await Recipe("01 Standard ohne Konflikt", d => Position(d, good));
        Guid alternativeB = await Recipe("02 Alternative B ohne Konflikt", d => Position(d, good2));
        await Recipe("03 Eindeutige Variante", d => Position(d, variant));
        await Recipe("04 Eindeutiger Ersatz", d => Position(d, bad, [good]));
        await Recipe("05 Zwei unabhaengige Aenderungen", d => { Position(d, bad, [good]); Position(d, variant); });
        await Recipe("06 Mehrdeutige Direktloesung", d => Position(d, variant, [good]));
        await Recipe("07 Partielle Loesung", d => { Position(d, bad, [good]); Position(d, bad); });
        Guid noSolution = await Recipe("08 Keine passende Loesung", d => Position(d, bad));
        Guid child = await Recipe("09 Subrecipe mit Variante", d => Position(d, variant));
        Guid nested = await Recipe("10 Verschachteltes Subrecipe", d => Child(d, child));
        await Recipe("11 Dasselbe Subrecipe zweimal", d => { Child(d, nested); Child(d, nested); });
        await Recipe("12 Subrecipe Ersatz", d => Child(d, noSolution, standard));
        await Recipe("13 Fixed und Stepwise Stoffmengen", d => { Position(d, quantitative, mode: ScalingMode.Fixed); Position(d, quantitative, mode: ScalingMode.Stepwise); });
        await Recipe("14 Qualitativ enthalten", d => Position(d, qualitative));
        await Recipe("15 Unknown pruefen", d => Position(d, unknown));
        await Recipe("16 Quantitativ linear", d => Position(d, quantitative));

        // Assertions against persisted, published data: fixture names must describe real resolver results.
        var supplyReferences = new EfSupplySuitabilityReferences(catering);
        var resolver = new SupplySolutionResolver(supplyReferences);
        var milkRequirements = new RequirementSignature(null, [milk], []);
        SupplySolution Solution(Guid revision) => new(Id("probe"), revision, null, 1,
            new Dictionary<string, RecipePositionSelection>(), [], SupplyDecisionSource.Automatic);
        SupplyResolution Resolve(int recipe, params Guid[] alternatives) => resolver.Resolve(Solution(recipes[recipe - 1].Revision), milkRequirements, alternatives);
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("Fixture assertion: " + label); }
        Check(Resolve(1).Selected is not null, "standard");
        Check(Resolve(3).Selected?.PositionSelections.Single().Value.VariantKey == "free", "unique variant");
        Check(Resolve(4).Selected?.PositionSelections.Count == 1, "unique replacement");
        Check(Resolve(5).Selected?.PositionSelections.Count == 2, "independent changes");
        Check(Resolve(6).Problems.Any(x => x.Code == MealPlanningProblemCode.MultipleDirectSolutions), "ambiguous direct");
        Check(Resolve(7).Problems.Any(x => x.Code == MealPlanningProblemCode.PartialDirectResolution), "partial direct");
        Check(Resolve(8).Selected is null, "no solution");
        Check(Resolve(8, standard).Stage == SupplyResolutionStage.OfferAlternative, "unique offer");
        Check(Resolve(8, standard, alternativeB).Problems.Any(x => x.Code == MealPlanningProblemCode.MultipleMatchingOfferAlternatives), "ambiguous offers");
        Check(Resolve(11).Selected?.PositionSelections.Count == 2, "repeated transitive occurrence paths");
        Check(Resolve(12).Selected?.PositionSelections.Count == 1, "subrecipe replacement");
        var evaluator = new SupplySuitabilityEvaluator(supplyReferences);
        Check(!evaluator.Evaluate(Solution(recipes[14].Revision), new(null, [], [new(lactose, 2)])).IsSuitable, "unknown not zero");
        Check(evaluator.Evaluate(Solution(recipes[15].Revision), new(null, [], [new(lactose, 2)])).IsSuitable &&
            !evaluator.Evaluate(Solution(recipes[15].Revision), new(null, [], [new(lactose, 0.5m)])).IsSuitable, "distinct synthetic thresholds");
        Console.WriteLine("PASS: published resolver fixture assertions.");

        var store = new MealPlanningStore(catering);
        var service = new MealPlanningService(store, camp, TimeProvider.System, camp, new EfSupplySuitabilityReferences(catering));
        Guid planId = Id("plan");
        Ensure(await store.CreateMealPlanAsync(new(planId, campId, "TEST Abnahmefaelle", 0)));
        var groups = new List<MealPlanOfferGroupDocument>();
        // Explicit day/type order, independent of GUID/database ordering.
        var orderedMeals = Enumerable.Range(0, 18).Select(i => meals.Single(x => x.Id == Id($"meal{i / 3}-{i % 3}"))).ToArray();
        for (int i = 0; i < 18; i++)
        {
            int recipeIndex = i == 0 || i > 15 ? 0 : i == 15 ? 7 : i + 1;
            Guid[] revisions = i == 6 ? [noSolution, standard] : i == 7 ? [noSolution, standard, alternativeB] : [recipes[recipeIndex].Revision];
            string label = i == 6 ? "Eine OfferGroup Alternative" : i == 7 ? "Mehrere OfferGroup Alternativen" : recipes[recipeIndex].Name;
            groups.Add(new(Id("offer" + i), orderedMeals[i].Id, "TEST " + label, 0,
                revisions.Select((r, n) => new MealPlanEntryDocument(Id($"entry{i}-{n}"), r, n == 0, null, MealPlanEntryRole.MainDish, "TEST", n)).ToArray()));
        }
        Ensure(await service.SaveMealPlanAsync(campId, planId, actor, new(0, "TEST Abnahmefaelle", 0, groups)));
        for (int i = 0; i < 3; i++)
        {
            var unit = new CookingUnit(Id("unit" + i), campId, "TEST " + new[] { "All - AlwaysTogether", "Special - Ask", "WithoutSpecial - NeverTogether" }[i], i, standardMealPlanId: planId);
            unit.SetParticipantFilter((CookingUnitParticipantFilter)i);
            unit.SetTogetherPreference(i == 0 ? CookingTogetherPreference.AlwaysTogether : i == 1 ? CookingTogetherPreference.Ask : CookingTogetherPreference.NeverTogether);
            catering.Add(unit); catering.Add(new CookingUnitStructureAssignment(Id("assignment" + i), campId, unit.Id, null, Id("node" + (i == 0 ? 0 : 1))));
        }
        await catering.SaveChangesAsync();
        Ensure(await store.SaveParticipantConfigurationAsync(campId, 0, MealPlanningDemandMode.UseActualParticipants, "[]", default));
        for (int comparison = 0; comparison < 3; comparison++)
        {
            var unit = new CookingUnit(Id("comparison-unit" + comparison), campId,
                "TEST Vergleich " + (CookingTogetherPreference)comparison, comparison + 3, standardMealPlanId: planId);
            unit.SetTogetherPreference((CookingTogetherPreference)comparison);
            catering.Add(unit);
            catering.Add(new CookingUnitStructureAssignment(Id("comparison-assignment" + comparison), campId,
                unit.Id, null, Id("comparison-node" + comparison)));
        }
        await catering.SaveChangesAsync();
        for (int i = 0; i < 3; i++) Ensure(await store.ConfigureCookingUnitMealAsync(campId, Id("unit" + i), orderedMeals[17].Id,
            new(MealPlanSubscriptionState.NoSupplyRequired, null, null, [], [])));
        for (int i = 0; i < 3; i++) foreach (var meal in orderedMeals) Ensure(await service.CalculateAsync(campId, Id("unit" + i), meal.Id));
        for (int i = 0; i < 3; i++) foreach (var meal in orderedMeals)
            Ensure(await service.CalculateAsync(campId, Id("comparison-unit" + i), meal.Id));
        var overview = (await service.GetOverviewAsync(campId))!;
        for (int i = 0; i < 3; i++)
        {
            // Exercise the actual service/persisted state, not just an isolated resolver.
            for (int pass = 0; pass < 2; pass++)
            {
                catering.ChangeTracker.Clear();
                if (pass == 1) Ensure(await service.CalculateAsync(campId, Id("comparison-unit" + i), Id("meal0-1")));
                var reloaded = (await service.GetOverviewAsync(campId))!;
                var supply = reloaded.OperationalMeals.Single(x => x.CookingUnitId == Id("comparison-unit" + i) && x.CampMealId == Id("meal0-1")).Supply!;
                Check(supply.Solutions.Count == (i == (int)CookingTogetherPreference.AlwaysTogether ? 1 : i == (int)CookingTogetherPreference.Ask ? 0 : 2), "preference solution count " + i);
                Check(supply.Problems.Any(x => x.Code == MealPlanningProblemCode.CommonSupplyDecisionRequired) == (i == (int)CookingTogetherPreference.Ask), "preference Ask " + i);
                Check(supply.Solutions.All(x => x.Source == SupplyDecisionSource.Automatic), "preference automatic provenance " + i);
                Check(supply.Solutions.Sum(x => x.Portions) == (i == (int)CookingTogetherPreference.Ask ? 0 : 4), "preference portions " + i);
                Check(supply.Problems.All(x => x.Code == MealPlanningProblemCode.CommonSupplyDecisionRequired ||
                    i == (int)CookingTogetherPreference.Ask && x.Code == MealPlanningProblemCode.RequirementGroupUndercovered), "preference suitability " + i);
                if (i == (int)CookingTogetherPreference.Ask)
                    Check(supply.CommonSupplyChoices!.Single().TogetherOptions.Any(x => x.Allocations.Count == 2 && x.Portions == 4) &&
                        supply.CommonSupplyChoices!.Single().SeparateSolutions.Count == 2, "preference explicit options");
            }
        }
        if (overview.OperationalMeals.Count != 108 || await camp.Participants.CountAsync() != 32 || overview.RecipeOptions.Count < 16)
            throw new InvalidOperationException("Seed completeness assertion failed.");
        Console.WriteLine("PASS: 32 participants, 16 published recipes, 18 meals, 6 kitchens, 108 calculated states; three preference comparisons.");
    }

    private static void Ensure(MealPlanningMutationResult result)
    {
        if (!result.IsSuccess) throw new InvalidOperationException("Seed planning: " + result);
    }
}
