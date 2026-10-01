namespace AnyRPG {
    public class InstantiatedRecipeItem : InstantiatedItem {

        private RecipeItem recipeItem = null;

        public InstantiatedRecipeItem(SystemGameManager systemGameManager, long instanceId, RecipeItem recipeItem, ItemQuality itemQuality) : base(systemGameManager, instanceId, recipeItem, itemQuality) {
            this.recipeItem = recipeItem;
        }

        public override bool IsUseable() {
            return true;
        }

        public override bool Use(UnitController sourceUnitController) {
            //Debug.Log(MyDisplayName + ".RecipeItem.Use()");
            if (sourceUnitController.CharacterRecipeManager.RecipeList.ContainsValue(recipeItem.Recipe)) {
                sourceUnitController.WriteMessageFeedMessage("Bu tarifi zaten biliyorsun!");
                return false;
            }
            //Debug.Log(MyDisplayName + ".RecipeItem.Use(): Player does not have the recipe: " + recipe.MyDisplayName);
            bool returnValue = base.Use(sourceUnitController);
            if (returnValue == false) {
                return false;
            }
            // check that the character is high enough level
            if (sourceUnitController.CharacterStats.Level < recipeItem.Recipe.RequiredLevel) {
                sourceUnitController.WriteMessageFeedMessage($"Bu tarifi öğrenmek için en az {recipeItem.Recipe.RequiredLevel}. seviye olmalısın");
                return false;
            }
            // check if the character has the right skill
            if (recipeItem.Recipe.Skill != null && sourceUnitController.CharacterSkillManager.HasSkill(recipeItem.Recipe.Skill) == false) {
                sourceUnitController.WriteMessageFeedMessage($"{recipeItem.Recipe.Skill.DisplayName} zanaatını bilmiyorsun");
                return false;
            }
            // check if the character has the required skill level
            if (recipeItem.Recipe.Skill != null && sourceUnitController.CharacterSkillManager.GetSkillLevel(recipeItem.Recipe.Skill) < recipeItem.Recipe.RequiredSkillLevel) {
                sourceUnitController.WriteMessageFeedMessage("Zanaat seviyen yeterince yüksek değil");
                return false;
            }

            // learn recipe if the character has the right ability
            if (sourceUnitController.CharacterAbilityManager.AbilityList.ContainsValue(recipeItem.Recipe.CraftAbility)) {
                sourceUnitController.CharacterRecipeManager.LearnRecipe(recipeItem.Recipe);
                sourceUnitController.WriteMessageFeedMessage($"{recipeItem.Recipe.DisplayName} tarifini öğrendin");
                Remove();
            } else {
                sourceUnitController.WriteMessageFeedMessage($"Bu tarifi öğrenmek için {recipeItem.Recipe.CraftAbility.DisplayName} bilmelisin!");
            }
            return returnValue;

        }


        public override string GetDescription() {
            //Debug.Log($"{item.ResourceName}.InstantiatedCurrencyItem.GetDescription()");

            return base.GetDescription() + recipeItem.GetRecipeItemDescription();
        }

    }

}