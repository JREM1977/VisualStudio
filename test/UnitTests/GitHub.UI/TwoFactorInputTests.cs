using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GitHub.UI;
using NUnit.Framework;

public class TwoFactorInputTests
{
    public class TheTextProperty : TestBaseClass
    {
#if !NCRUNCH
        [Test]
#endif
        public void SetsTextBoxesToIndividualCharacters()
        {
            var twoFactorInput = new TwoFactorInput();
            var textBoxes = GetChildrenRecursive(twoFactorInput).OfType<TextBox>().ToList();

            twoFactorInput.Text = "012345";

            Assert.AreEqual("012345", twoFactorInput.Text);
            Assert.AreEqual("0", textBoxes[0].Text);
            Assert.AreEqual("1", textBoxes[1].Text);
            Assert.AreEqual("2", textBoxes[2].Text);
            Assert.AreEqual("3", textBoxes[3].Text);
            Assert.AreEqual("4", textBoxes[4].Text);
            Assert.AreEqual("5", textBoxes[5].Text);
        }

#if !NCRUNCH
        [Test]
#endif
        public void IgnoresNonDigitCharacters()
        {
            var twoFactorInput = new TwoFactorInput();
            var textBoxes = GetChildrenRecursive(twoFactorInput).OfType<TextBox>().ToList();

            twoFactorInput.Text = "01xyz2345";

            Assert.AreEqual("012345", twoFactorInput.Text);
            Assert.AreEqual("0", textBoxes[0].Text);
            Assert.AreEqual("1", textBoxes[1].Text);
            Assert.AreEqual("2", textBoxes[2].Text);
            Assert.AreEqual("3", textBoxes[3].Text);
            Assert.AreEqual("4", textBoxes[4].Text);
            Assert.AreEqual("5", textBoxes[5].Text);
        }

#if !NCRUNCH
        [Test]
#endif
        public void HandlesNotEnoughCharacters()
        {
            var twoFactorInput = new TwoFactorInput();
            var textBoxes = GetChildrenRecursive(twoFactorInput).OfType<TextBox>().ToList();

            twoFactorInput.Text = "012";

            Assert.AreEqual("012", twoFactorInput.Text);
            Assert.AreEqual("0", textBoxes[0].Text);
            Assert.AreEqual("1", textBoxes[1].Text);
            Assert.AreEqual("2", textBoxes[2].Text);
            Assert.AreEqual("", textBoxes[3].Text);
            Assert.AreEqual("", textBoxes[4].Text);
            Assert.AreEqual("", textBoxes[5].Text);
        }

#if !NCRUNCH
#endif
        [TestCase(null, null)]
        [TestCase("", "")]
        [TestCase("xxxx", "")]
        public void HandlesNullAndStringsWithNoDigits(string input, string expected)
        {
            var twoFactorInput = new TwoFactorInput();
            var textBoxes = GetChildrenRecursive(twoFactorInput).OfType<TextBox>().ToList();

            twoFactorInput.Text = input;

            Assert.AreEqual(expected, twoFactorInput.Text);
            Assert.AreEqual("", textBoxes[0].Text);
            Assert.AreEqual("", textBoxes[1].Text);
            Assert.AreEqual("", textBoxes[2].Text);
            Assert.AreEqual("", textBoxes[3].Text);
            Assert.AreEqual("", textBoxes[4].Text);
            Assert.AreEqual("", textBoxes[5].Text);
        }

        static IEnumerable<FrameworkElement> GetChildrenRecursive(FrameworkElement element)
        {
            yield return element;
            foreach (var child in LogicalTreeHelper.GetChildren(element)
                .Cast<FrameworkElement>()
                .SelectMany(GetChildrenRecursive))
            {
                yield return child;
            }
        }
    }
}
