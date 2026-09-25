using NUnit.Framework;
using UnityEngine.UIElements;

namespace Tagtag.UI.Tests
{
    public sealed class PaperPrimitiveTests
    {
        [Test]
        public void FieldCountsDraftCharactersAndExposesValidationWithoutLosingItsValue()
        {
            PaperField field = new PaperField("Clue", "Three words", 180, false, "A hint visitors can see", true);
            Assert.AreEqual(180, field.maxLength);
            Assert.AreEqual("11 / 180", field.Q<Label>("field-counter").text);
            field.PresentError("Add a clue");
            Assert.AreEqual("Three words", field.value);
            Assert.AreEqual(DisplayStyle.Flex, field.Q<Label>("field-error").style.display.value);
            field.PresentError("");
            Assert.AreEqual(DisplayStyle.None, field.Q<Label>("field-error").style.display.value);
        }

        [Test]
        public void SelectionKeepsAStableButtonAndUpdatesItsSelectedState()
        {
            PaperSelection selection = new PaperSelection("Taggi pose one", false, null);
            Assert.IsFalse(selection.IsSelected);
            selection.SetSelected(true);
            Assert.IsTrue(selection.IsSelected);
            Assert.IsTrue(selection.ClassListContains("selected"));
            selection.SetSelected(false);
            Assert.IsFalse(selection.ClassListContains("selected"));
        }

        [Test]
        public void FieldSupportTextScalesWithItsInput()
        {
            PaperField field = new PaperField("Note", "Hello", 2000, true, "Unlocked after discovery", true);
            field.ApplyScale(1.4f);
            Assert.AreEqual(22f, field.Q<Label>(className: "unity-base-field__label").style.fontSize.value.value);
            Assert.AreEqual(18f, field.Q<Label>("field-counter").style.fontSize.value.value);
            Assert.AreEqual(18f, field.Q<Label>(className: "field-helper").style.fontSize.value.value);
        }

        [TestCase(100f, 0f, false)]
        [TestCase(100f, 50f, false)]
        [TestCase(100f, 70f, true)]
        [TestCase(400f, 100f, true)]
        public void SheetDismissalRequiresAnIntentionalDownwardPull(float height, float distance, bool expected)
        {
            Assert.AreEqual(expected, PaperSheet.ShouldDismiss(height, distance, 0f));
        }
    }
}
