using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

/// <summary>Describes variant coverage for one conflict across every occurrence in a published recipe graph.</summary>
public static class RecipeVariantConflictSummary
{
    public static IReadOnlyList<string> Find(RecipeSnapshot snapshot, ConflictReference conflict,
        Func<Guid, RecipeSnapshot?> resolveRecipe,
        IReadOnlyDictionary<Guid, RecipeEditorIngredientReference> ingredients)
    {
        var path = new HashSet<Guid>();
        return Visit(snapshot);

        string[] Visit(RecipeSnapshot current)
        {
            var names = new List<string>();
            bool found = false;
            foreach (var position in current.IngredientPositions.Where(value => value.Ingredient.Conflicts.Contains(conflict)))
            {
                found = true;
                var variants = ingredients.GetValueOrDefault(position.Ingredient.IngredientRevisionId)?.Conflicts
                    .FirstOrDefault(value => value.Type == conflict.Type && value.Id == conflict.Id)?.PreventableByVariants;
                if (variants is null || variants.Count == 0) return [];
                names.AddRange(variants.Select(value => $"{position.Ingredient.Name}: {value}"));
            }
            foreach (var position in current.SubrecipePositions.Where(value => value.RevisionConflicts.Contains(conflict)))
            {
                found = true;
                if (!path.Add(position.RecipeRevisionId)) return [];
                var child = resolveRecipe(position.RecipeRevisionId);
                var variants = child is null ? [] : Visit(child);
                path.Remove(position.RecipeRevisionId);
                if (variants.Length == 0) return [];
                names.AddRange(variants);
            }
            return found ? names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray() : [];
        }
    }
}
