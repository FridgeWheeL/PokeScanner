using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PokeScanner.Tests
{
    [TestClass]
    public class ApiTests
    {
        private const string TestCardName = "Pikachu";
        private const string TestCardId = "001-001";

        [TestMethod]
        public async Task SearchCardsAsync_ShouldReturnResults_WhenCardExists()
        {
            var mockApiService = new Mock<ITcgdexApiService>();
            mockApiService.Setup(x => x.SearchCardsAsync(TestCardName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CardResult>
                {
                    new CardResult
                    {
                        CardId = TestCardId,
                        Name = TestCardName,
                        Number = "",
                        SetName = "",
                        Hp = "",
                        Score = 0
                    }
                });

            var cards = await mockApiService.Object.SearchCardsAsync(TestCardName);

            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual(TestCardId, cards[0].CardId);
            Assert.AreEqual(TestCardName, cards[0].Name);
        }

        [TestMethod]
        public async Task SearchCardsAsync_ShouldReturnEmpty_WhenCardNotFound()
        {
            var mockApiService = new Mock<ITcgdexApiService>();
            mockApiService.Setup(x => x.SearchCardsAsync(TestCardName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CardResult>());

            var cards = await mockApiService.Object.SearchCardsAsync(TestCardName);

            Assert.AreEqual(0, cards.Count);
        }

        [TestMethod]
        public async Task SearchCardsAsync_ShouldHandleNetworkTimeout()
        {
            var mockApiService = new Mock<ITcgdexApiService>();
            mockApiService.Setup(x => x.SearchCardsAsync(TestCardName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TaskCanceledException("The request timed out"));

            await Assert.ThrowsAsync<TaskCanceledException>(
                () => mockApiService.Object.SearchCardsAsync(TestCardName)
            );
        }

        [TestMethod]
        public async Task SearchCardsAsync_ShouldRespectCancellationToken()
        {
            var mockApiService = new Mock<ITcgdexApiService>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            mockApiService.Setup(x => x.SearchCardsAsync(TestCardName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => mockApiService.Object.SearchCardsAsync(TestCardName, cts.Token)
            );
        }
    }
}
