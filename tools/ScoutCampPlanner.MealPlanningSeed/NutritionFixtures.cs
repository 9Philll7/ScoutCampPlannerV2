using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;

namespace ScoutCampPlanner.MealPlanningSeed;

internal static class NutritionFixtures
{
    private const string Source = "TEST synthetische Nährwerte zur UI-Abnahme; keine Lebensmitteldaten oder Ernährungsempfehlung";
    private static Guid Id(string key) => AcceptanceSeed.Id("nutrition-fixtures-v1/" + key);

    internal static async Task AddAsync(CateringDbContext database)
    {
        Guid camp = AcceptanceSeed.Id("camp"), actor = AcceptanceSeed.Id("actor");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var references = await new IngredientEditorReferenceDataStore(database).GetAsync();
        Guid gram = references.Units.Single(value => value.Symbol == "g").Id;
        Guid milk = references.Allergens.Single(value => value.Code == "MILK").Id;
        Guid lactose = references.Intolerances.Single(value => value.Code == "LACTOSE").Id;
        var ingredientStore = new IngredientRevisionWorkflowStore(database);
        int createdIngredients = 0, createdRecipes = 0;
        IngredientNutritionProfile Profile(decimal kcal, decimal fat, decimal saturated, decimal carbs,
            decimal sugars, decimal protein, decimal salt, decimal? fiber, bool reviewed = true) =>
            new(100, gram, kcal * 4.184m, fat, saturated, carbs, sugars, protein, salt, fiber,
                IngredientNutritionSourceType.ManualEstimate, Source,
                reviewed ? IngredientNutritionReviewState.Reviewed : IngredientNutritionReviewState.Unreviewed,
                new DateOnly(2026, 11, 20));
        async Task<Guid> Ingredient(string key, string label, IngredientNutritionProfile? profile, bool dairy = false)
        {
            Guid revisionId = Id("ingredient-revision/" + key);
            var existing = await ingredientStore.GetAsync(revisionId);
            if (existing?.State == IngredientRevisionState.Published) return revisionId;
            if (existing is not null && (existing.RowVersion != 1 || existing.Name != "TEST Nährwerte " + label || existing.SourceSummary != Source))
                throw new InvalidOperationException("Existing edited fixture draft retained: " + label);
            var content = IngredientRevisionDraftContent.Create("TEST Nährwerte " + label,
                references.Categories.Single(value => value.Code == "OTHER").Id, gram,
                IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed,
                allergens: references.Allergens.Select(value => new IngredientPropertyValue(value.Id,
                    dairy && value.Id == milk ? IngredientPropertyState.Contains : IngredientPropertyState.DoesNotContain,
                    IngredientPropertySource.ManuallyVerified)),
                intolerances: references.Intolerances.Select(value => new IngredientPropertyValue(value.Id,
                    dairy && value.Id == lactose ? IngredientPropertyState.Contains : IngredientPropertyState.DoesNotContain,
                    IngredientPropertySource.ManuallyVerified)),
                origins: [new(references.Origins.Single(value => value.Code == (dairy ? "DAIRY" : "PLANT")).Id,
                    IngredientPropertyState.Contains, IngredientPropertySource.ManuallyVerified)],
                nutritionProfile: profile, sourceSummary: Source);
            long version = existing?.RowVersion ?? 1;
            if (existing is null)
            {
                var created = await ingredientStore.CreateDraftAsync(Id("ingredient/" + key), revisionId,
                    new(IngredientScopeType.Camp, camp), content, actor, now);
                if (created.Status != IngredientRevisionMutationStatus.Created) throw new InvalidOperationException(label + ": " + created.Status);
                version = created.RowVersion!.Value;
            }
            // Match the separate request scopes of the regular API workflow.
            database.ChangeTracker.Clear();
            var saved = await ingredientStore.SaveDraftAsync(revisionId, content, version, actor, now);
            if (saved.Status != IngredientRevisionMutationStatus.Saved) throw new InvalidOperationException(label + ": " + saved.Status);
            database.ChangeTracker.Clear();
            var published = await ingredientStore.PublishAsync(revisionId, saved.RowVersion!.Value, actor, now);
            if (published.Status != IngredientRevisionMutationStatus.Published) throw new InvalidOperationException(label + ": " + published.Status);
            database.ChangeTracker.Clear(); createdIngredients++; return revisionId;
        }
        Guid oats = await Ingredient("oats", "Haferflocken", Profile(360, 7, 1.2m, 58, 1, 13, 0.02m, 10));
        Guid dairy = await Ingredient("milk", "Milch", Profile(64, 3.5m, 2.3m, 4.8m, 4.8m, 3.4m, 0.1m, 0), true);
        Guid apple = await Ingredient("apple", "Apfel", Profile(52, 0.2m, 0, 12, 10, 0.3m, 0, 2.4m));
        Guid oil = await Ingredient("oil", "Rapsöl", Profile(900, 100, 7, 0, 0, 0, 0, 0));
        Guid sugar = await Ingredient("sugar", "Zucker", Profile(400, 0, 0, 100, 100, 0, 0, 0));
        Guid noFiber = await Ingredient("sugar-no-fiber", "Zucker ohne Ballaststoffangabe", Profile(400, 0, 0, 100, 100, 0, 0, null));
        Guid missing = await Ingredient("spice", "Gewürz ohne Profil", null);
        Guid unreviewed = await Ingredient("yoghurt", "Joghurt ungeprüft", Profile(60, 3, 2, 5, 5, 3, 0.1m, 0, false), true);
        var drafts = new RecipeDraftStore(database);
        var recipeReferences = new EfRecipeReferences(database);
        var validator = new RecipePublicationValidator(recipeReferences);
        var builder = new RecipeSnapshotBuilder(recipeReferences);
        var publisher = new RecipePublisher(database, drafts, validator, builder);
        var calculator = new RecipeCalculator(recipeReferences);
        async Task Recipe(string key, string label, Action<RecipeDraft> configure, decimal? expectedKcal, int? expectedMissing = null, bool publish = true)
        {
            Guid id = Id("recipe/" + key);
            if (await drafts.FindAsync(id) is not null) { Console.WriteLine("Retained: TEST Nährwerte " + label); return; }
            var draft = new RecipeDraft(id, RecipeScopeType.Camp, camp, RecipeType.PortionBased, "TEST Nährwerte " + label);
            draft.ConfigurePortionReference(10, false); draft.SetDetails(Source, Source, "TEST"); configure(draft);
            await new RecipeEditorStore(database, drafts).CreateCampAsync(draft, camp, actor, now);
            if (publish)
            {
                var result = await publisher.PublishAsync(id, 0, actor, now, true);
                if (result.Status != RecipePublicationStatus.Published) throw new InvalidOperationException(label + ": " + result.Status);
                var calculation = calculator.Calculate(new(result.Revision!.Id, 10, 10));
                if (expectedKcal.HasValue && (!calculation.Nutrition.IsComplete || calculation.Nutrition.Total?.EnergyKilocalories != expectedKcal))
                    throw new InvalidOperationException(label + ": unexpected energy total.");
                if (expectedMissing.HasValue && (calculation.Nutrition.IsComplete || calculation.Nutrition.MissingContributions.Count != expectedMissing))
                    throw new InvalidOperationException(label + ": unexpected missing profiles.");
                Console.WriteLine($"Verified: {draft.Name}; {draft.IngredientPositions.Count} ingredients; kcal={calculation.Nutrition.Total?.EnergyKilocalories}; missing={calculation.Nutrition.MissingContributions.Count}");
            }
            else
            {
                var result = validator.Validate(draft);
                if (key == "errors" && result.Errors.Count == 0 || key == "warnings" && (result.Errors.Count != 0 || result.Warnings.Count < 2))
                    throw new InvalidOperationException(label + ": unexpected validation state.");
                Console.WriteLine($"Verified draft: {draft.Name}; errors={result.Errors.Count}; warnings={result.Warnings.Count}");
            }
            createdRecipes++;
        }
        void Position(RecipeDraft draft, Guid ingredient, decimal? quantity, string groupName = "Basis",
            ScalingMode mode = ScalingMode.Linear, decimal? minimum = null, decimal? maximum = null)
        {
            Guid groupId = Id(draft.Id + "/group/" + groupName);
            if (!draft.Groups.Any(value => value.Id == groupId)) draft.AddGroup(new(groupId, draft.Id, groupName, draft.Groups.Count));
            draft.AddIngredientPosition(new(Id(draft.Id + "/position/" + draft.IngredientPositions.Count), draft.Id, groupId,
                ingredient, quantity, gram, draft.IngredientPositions.Count, mode, AgeGroupScalingMode.Ignore,
                mode == ScalingMode.Stepwise ? new(5, 10) : null, minimum, maximum));
        }
        void Porridge(RecipeDraft draft) { Position(draft, oats, 500); Position(draft, dairy, 1000); Position(draft, apple, 500); Position(draft, oil, 20, "Verfeinern"); Position(draft, sugar, 50, "Verfeinern"); }
        await Recipe("complete", "01 Haferbrei vollständig", Porridge, 3080);
        await Recipe("incomplete", "02 Haferbrei unvollständig", draft => { Porridge(draft); Position(draft, missing, 5, "Verfeinern"); Position(draft, unreviewed, 200, "Verfeinern"); }, null, 2);
        await Recipe("fiber", "03 Obstsalat Ballaststoffe offen", draft => { Position(draft, apple, 1000); Position(draft, dairy, 500); Position(draft, noFiber, 100, "Verfeinern"); }, 1240);
        await Recipe("scaling", "04 Mengenregeln", draft => { Position(draft, oats, 500, minimum: 300, maximum: 700); Position(draft, dairy, 1000); Position(draft, oil, 20, "Verfeinern", ScalingMode.Fixed); Position(draft, sugar, 1, "Verfeinern", ScalingMode.Stepwise); }, 2700);
        await Recipe("errors", "05 Entwurf mit Fehlern", draft => { Position(draft, oats, 100); Position(draft, dairy, null); Position(draft, apple, 50); draft.SetDetails(null, null, "TEST: Menge bei Milch fehlt absichtlich."); }, null, publish: false);
        await Recipe("warnings", "06 Entwurf mit Warnungen", draft => { Position(draft, oats, 100); Position(draft, dairy, 200); Position(draft, apple, 100); draft.SetDetails(null, null, "TEST: Beschreibung und Quelle fehlen absichtlich."); }, null, publish: false);
        Console.WriteLine($"PASS: added {createdIngredients} nutrition ingredients and {createdRecipes} multi-ingredient recipes; existing data retained.");
    }
}
