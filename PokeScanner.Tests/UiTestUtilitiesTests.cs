using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.RegularExpressions;

namespace PokeScanner.Tests;

public static class UiTestUtilities
{
    public static string CleanCardName(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return "";
        var replaced = rawText.Replace("(", "At").Replace(")", "");
        var cleaned = Regex.Replace(replaced, "[^\\w\\s-]", "").Trim();
        return Regex.Replace(cleaned, "\\s+", " ");
    }
}

/// <summary>
/// Tests card name normalization from OCR/LLM input.
/// </summary>
[TestClass]
public class UiTestUtilitiesTests
{
    [TestMethod]
    [DataRow("shiny * card", "shiny card")] // Basic removal of symbols
    [DataRow("Pokémon Card: PiKACHU!! SET XYZ!", "Pokémon Card PiKACHU SET XYZ")] // Multiple punctuation/capitalization test
    [DataRow("  Mega Charizard V------ W ", "Mega Charizard V------ W")] // Extra spaces and dashes
    [DataRow("Char(SET)", "CharAtSET")] // Test parentheses.
    [DataRow("!!!@#$%", "")] // All junk removed
    public void CleanCardName_ShouldNormalizeInput(string rawInput, string expectedOutput)
    {
        string actual = UiTestUtilities.CleanCardName(rawInput);
        Assert.AreEqual(expectedOutput, actual);
    }

    [TestMethod]
    public void CleanCardName_ShouldHandleEmptyOrNullInput()
    {
        string empty = UiTestUtilities.CleanCardName(null!);
        string whitespace = UiTestUtilities.CleanCardName("   ");
        Assert.AreEqual("", empty);
        Assert.AreEqual("", whitespace);
    }

    [TestMethod]
    public void CleanCardName_ShouldConvertParenthesesCorrectly()
    {
        string actual = UiTestUtilities.CleanCardName("Pikachu (V)");
        Assert.AreEqual("Pikachu AtV", actual);
    }
}
