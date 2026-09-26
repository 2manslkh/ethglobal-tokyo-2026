using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI.Tests
{
    public sealed class PaperPrimitiveTests
    {
        [Test]
        public void ActionButtonsKeepOneDieCutOutlineIncludingCircularOverride()
        {
            PaperButton primary = new PaperButton("Explore nearby", null, PaperButtonKind.Primary);
            PaperButton secondary = new PaperButton("Previous", null, PaperButtonKind.Secondary);
            Assert.AreEqual(0, primary.Children().OfType<PaperDottedOutline>().Count());
            Assert.AreEqual(0, secondary.Children().OfType<PaperDottedOutline>().Count());
            PaperDottedOutline.DecorateCircular(primary);
            Assert.AreEqual(0, primary.Children().OfType<PaperDottedOutline>().Count());
            Assert.IsTrue(primary.ClassListContains("paper-die-cut"));
        }
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
        public void CollectedCountUpdatesNumberAndCaptionWithoutReplacingTheLabels()
        {
            PaperCollectionCount count = new PaperCollectionCount();
            Label number = count.Q<Label>("home-collected-number");
            Label caption = count.Q<Label>("home-collected-label");

            Assert.IsNotNull(number);
            Assert.IsNotNull(caption);
            Assert.AreEqual("0", number.text);
            Assert.AreEqual("stickers collected", caption.text);

            count.SetCount(1);
            Assert.AreSame(number, count.Q<Label>("home-collected-number"));
            Assert.AreSame(caption, count.Q<Label>("home-collected-label"));
            Assert.AreEqual("1", number.text);
            Assert.AreEqual("sticker collected", caption.text);

            count.SetCount(23);
            Assert.AreEqual("23", number.text);
            Assert.AreEqual("stickers collected", caption.text);
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

        [Test]
        public void HandwrittenFontIsReservedForTitlesAndBodyFacesRemainAvailable()
        {
            Assert.AreEqual(PaperTextRole.Display, PaperTypography.Role(30, true));
            Assert.AreEqual(PaperTextRole.Heading, PaperTypography.Role(21, true));
            Assert.AreEqual(PaperTextRole.Emphasis, PaperTypography.Role(18, true));
            Assert.AreEqual(PaperTextRole.Body, PaperTypography.Role(16, false));
            Assert.AreEqual(34, PaperTypography.PointSize(30, PaperTextRole.Display));
            Assert.AreEqual(18, PaperTypography.PointSize(18, PaperTextRole.Emphasis));
            Assert.IsNotNull(Resources.Load<Font>("Tagtag/Fonts/ShadowsIntoLight"));
            Assert.IsNotNull(Resources.Load<Font>("Tagtag/Fonts/InstrumentRegular"));
            Assert.IsNotNull(Resources.Load<Font>("Tagtag/Fonts/InstrumentSemibold"));
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
