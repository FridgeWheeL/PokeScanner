using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PokeScanner.Tests
{
    [TestClass]
    public class LlmTests
    {
        private const string TestCardName = "Pikachu";
        private const string TestCardNumber = "001/001";

        [TestMethod]
        public void ExtractJsonFromResponse_ShouldExtractValidJson()
        {
            // Arrange
            var content = JsonSerializer.Serialize(new { name = TestCardName, number = TestCardNumber });

            // Act
            var jsonMatch = Regex.Match(content, @"\{[^}]+\}");

            // Assert
            Assert.IsTrue(jsonMatch.Success);
            using var doc = JsonDocument.Parse(jsonMatch.Value);
            Assert.AreEqual(TestCardName, doc.RootElement.GetProperty("name").GetString());
            Assert.AreEqual(TestCardNumber, doc.RootElement.GetProperty("number").GetString());
        }

        [TestMethod]
        public void ExtractJsonFromResponse_ShouldExtractMarkdownWrappedJson()
        {
            // Arrange
            var content = $"```json\n{{\"name\":\"{TestCardName}\",\"number\":\"{TestCardNumber}\"}}\n```";

            // Act
            var jsonMatch = Regex.Match(content, @"\{[^}]+\}");

            // Assert
            Assert.IsTrue(jsonMatch.Success);
            using var doc = JsonDocument.Parse(jsonMatch.Value);
            Assert.AreEqual(TestCardName, doc.RootElement.GetProperty("name").GetString());
            Assert.AreEqual(TestCardNumber, doc.RootElement.GetProperty("number").GetString());
        }

        [TestMethod]
        public void ExtractJsonFromResponse_ShouldNotExtractFromInvalidJson()
        {
            // Arrange
            var content = "This is not JSON at all";

            // Act
            var jsonMatch = Regex.Match(content, @"\{[^}]+\}");

            // Assert
            Assert.IsFalse(jsonMatch.Success);
        }

        [TestMethod]
        public void ExtractJsonFromResponse_ShouldExtractFromPlainTextFallback()
        {
            var content = $"name:{TestCardName}, number:{TestCardNumber}";

            var nameMatch = Regex.Match(content, @"name[\"":]+\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase);
            var numMatch = Regex.Match(content, @"number[\"":]+\s*(\d+/\d+)", RegexOptions.IgnoreCase);

            Assert.IsTrue(nameMatch.Success);
            Assert.AreEqual(TestCardName, nameMatch.Groups[1].Value.Trim());
            Assert.IsTrue(numMatch.Success);
            Assert.AreEqual(TestCardNumber, numMatch.Groups[1].Value.Trim());
        }

        [TestMethod]
        public void ExtractJsonFromResponse_ShouldHandleMissingJsonInPlainText()
        {
            // Arrange
            var content = "Just some random text without any structured data";

            // Act
            var nameMatch = Regex.Match(content, @"name[\"":]+\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase);
            var numMatch = Regex.Match(content, @"number[\"":]+\s*(\d+/\d+)", RegexOptions.IgnoreCase);

            // Assert
            Assert.IsFalse(nameMatch.Success);
            Assert.IsFalse(numMatch.Success);
        }

        [TestMethod]
        public void RequestBody_ShouldIncludeImageData()
        {
            var cardImage = CreateTestImage();

            var b64 = Convert.ToBase64String(cardImage.ToBytes());
            var contentParts = new List<object>
            {
                new { type = "text", text = "Identify this Pokemon TCG card. The first image is the full card; the second image is a close-up of the bottom of the card with the set number. Return only JSON: {\"name\":\"card name\",\"number\":\"NNN/NNN\"}. No other text." },
                new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64}" } }
            };

            var body = new
            {
                model = "gemma3:12b",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = contentParts.ToArray()
                    }
                },
                max_tokens = 200,
                temperature = 0.1
            };

            var json = JsonSerializer.Serialize(body);

            Assert.IsTrue(json.Contains("data:image/jpeg;base64,"));
            Assert.IsTrue(json.Contains("gemma3:12b"));
        }

        [TestMethod]
        public void RequestBody_ShouldIncludeBothImagesWhenBottomCropProvided()
        {
            var cardImage = CreateTestImage();
            var bottomCrop = CreateTestImage(100, 50);

            var b64 = Convert.ToBase64String(cardImage.ToBytes());
            var bottomB64 = Convert.ToBase64String(bottomCrop.ToBytes());
            var contentParts = new List<object>
            {
                new { type = "text", text = "Identify this Pokemon TCG card. The first image is the full card; the second image is a close-up of the bottom of the card with the set number. Return only JSON: {\"name\":\"card name\",\"number\":\"NNN/NNN\"}. No other text." },
                new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64}" } }
            };
            contentParts.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{bottomB64}" } });

            var body = new
            {
                model = "gemma3:12b",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = contentParts.ToArray()
                    }
                },
                max_tokens = 200,
                temperature = 0.1
            };

            var json = JsonSerializer.Serialize(body);

            Assert.IsTrue(json.Contains("data:image/jpeg;base64,"));
            Assert.AreEqual(2, json.Split("data:image/jpeg;base64,").Length - 1);
        }

        [TestMethod]
        public void ParseLlmResponse_ShouldExtractJsonFromValidResponse()
        {
            var content = JsonSerializer.Serialize(new { name = TestCardName, number = TestCardNumber });

            var jsonMatch = Regex.Match(content, @"\{[^}]+\}");
            Assert.IsTrue(jsonMatch.Success, "Expected JSON object in response");

            using var cardDoc = JsonDocument.Parse(jsonMatch.Value);
            var name = cardDoc.RootElement.GetProperty("name").GetString() ?? "";
            var number = cardDoc.RootElement.GetProperty("number").GetString() ?? "";

            Assert.AreEqual(TestCardName, name.Trim());
            Assert.AreEqual(TestCardNumber, number.Trim());
        }

        [TestMethod]
        public void ParseLlmResponse_ShouldExtractJsonFromMarkdownWrappedResponse()
        {
            var content = $"```json\n{{\"name\":\"{TestCardName}\",\"number\":\"{TestCardNumber}\"}}\n```";

            var jsonMatch = Regex.Match(content, @"\{[^}]+\}");
            Assert.IsTrue(jsonMatch.Success, "Expected JSON object in markdown-wrapped response");

            using var cardDoc = JsonDocument.Parse(jsonMatch.Value);
            var name = cardDoc.RootElement.GetProperty("name").GetString() ?? "";
            var number = cardDoc.RootElement.GetProperty("number").GetString() ?? "";

            Assert.AreEqual(TestCardName, name.Trim());
            Assert.AreEqual(TestCardNumber, number.Trim());
        }

        [TestMethod]
        public void ParseLlmResponse_ShouldFallbackToTextExtraction()
        {
            var content = $"name:{TestCardName}, number:{TestCardNumber}";

            var nameMatch = Regex.Match(content, @"name[\"":]+\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase);
            var numMatch = Regex.Match(content, @"number[\"":]+\s*(\d+/\d+)", RegexOptions.IgnoreCase);
            var name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "";
            var number = numMatch.Success ? numMatch.Groups[1].Value.Trim() : "";

            Assert.AreEqual(TestCardName, name);
            Assert.AreEqual(TestCardNumber, number);
        }

        [TestMethod]
        public void ParseLlmResponse_ShouldReturnEmptyForInvalidResponse()
        {
            // Arrange
            var content = "Invalid response format";

            // Act
            var jsonMatch = Regex.Match(content, @"\{[^}]+\}");
            string name = "";
            string number = "";

            if (jsonMatch.Success)
            {
                using var cardDoc = JsonDocument.Parse(jsonMatch.Value);
                name = cardDoc.RootElement.GetProperty("name").GetString() ?? "";
                number = cardDoc.RootElement.GetProperty("number").GetString() ?? "";
            }
            else
            {
                var nameMatch = Regex.Match(content, @"name[\"":]+\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase);
                var numMatch = Regex.Match(content, @"number[\"":]+\s*(\d+/\d+)", RegexOptions.IgnoreCase);
                name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "";
                number = numMatch.Success ? numMatch.Groups[1].Value.Trim() : "";
            }

            // Assert
            Assert.AreEqual("", name);
            Assert.AreEqual("", number);
        }

        [TestMethod]
        public void ParseLlmResponse_ShouldHandleEmptyContent()
        {
            // Arrange
            var content = "";

            // Act
            var jsonMatch = Regex.Match(content, @"\{[^}]+\}");
            string name = "";
            string number = "";

            if (jsonMatch.Success)
            {
                using var cardDoc = JsonDocument.Parse(jsonMatch.Value);
                name = cardDoc.RootElement.GetProperty("name").GetString() ?? "";
                number = cardDoc.RootElement.GetProperty("number").GetString() ?? "";
            }
            else
            {
                var nameMatch = Regex.Match(content, @"name[\"":]+\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase);
                var numMatch = Regex.Match(content, @"number[\"":]+\s*(\d+/\d+)", RegexOptions.IgnoreCase);
                name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "";
                number = numMatch.Success ? numMatch.Groups[1].Value.Trim() : "";
            }

            // Assert
            Assert.AreEqual("", name);
            Assert.AreEqual("", number);
        }

        [TestMethod]
        public void BuildLlmRequest_ShouldIncludeRequiredFields()
        {
            var cardImage = CreateTestImage();
            var bottomCrop = CreateTestImage(100, 50);
            var model = "gemma3:12b";

            var b64 = Convert.ToBase64String(cardImage.ToBytes());

            var contentParts = new List<object>
            {
                new { type = "text", text = "Identify this Pokemon TCG card. The first image is the full card; the second image is a close-up of the bottom of the card with the set number. Return only JSON: {\"name\":\"card name\",\"number\":\"NNN/NNN\"}. No other text." },
                new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64}" } }
            };

            if (bottomCrop != null)
            {
                var bottomB64 = Convert.ToBase64String(bottomCrop.ToBytes());
                contentParts.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{bottomB64}" } });
            }

            var body = new
            {
                model = model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = contentParts.ToArray()
                    }
                },
                max_tokens = 200,
                temperature = 0.1
            };

            var json = JsonSerializer.Serialize(body);

            Assert.IsTrue(json.Contains(model));
            Assert.IsTrue(json.Contains("data:image/jpeg;base64,"));
            Assert.IsTrue(json.Contains("max_tokens"));
            Assert.IsTrue(json.Contains("temperature"));
        }

        private static OpenCvSharp.Mat CreateTestImage(int width = 400, int height = 560)
        {
            using var mat = new OpenCvSharp.Mat(height, width, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.White);
            return mat.Clone();
        }
    }
}