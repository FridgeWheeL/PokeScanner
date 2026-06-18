using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PokeScanner
{
    public interface ITcgdexApiService
    {
        Task<List<CardResult>> SearchCardsAsync(string cardName, CancellationToken ct = default);
    }

    public class TcgdexApiService : ITcgdexApiService
    {
        private readonly HttpClient _http;

        public TcgdexApiService(HttpClient httpClient)
        {
            _http = httpClient;
        }

        public async Task<List<CardResult>> SearchCardsAsync(string cardName, CancellationToken ct = default)
        {
            var results = new List<CardResult>();
            try
            {
                ct.ThrowIfCancellationRequested();
                var searchUrl = $"https://api.tcgdex.net/v2/en/cards?name={Uri.EscapeDataString(cardName)}";

                var response = await _http.GetAsync(searchUrl, ct);
                if (!response.IsSuccessStatusCode) return results;

                var briefJson = await response.Content.ReadAsStringAsync(ct);
                using var briefDoc = JsonDocument.Parse(briefJson);
                var briefCards = briefDoc.RootElement.EnumerateArray().ToList();

                if (briefCards.Count == 0) return results;

                var lockObj = new object();
                var sem = new SemaphoreSlim(3);
                var tasks = briefCards.Select(async brief =>
                {
                    var cardId = brief.GetProperty("id").GetString() ?? "";
                    if (string.IsNullOrEmpty(cardId)) return;

                    await sem.WaitAsync(ct);
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        var fullUrl = $"https://api.tcgdex.net/v2/en/cards/{cardId}";
                        var fullResponse = await _http.GetAsync(fullUrl, ct);
                        if (!fullResponse.IsSuccessStatusCode) return;

                        var fullJson = await fullResponse.Content.ReadAsStringAsync(ct);
                        using var fullDoc = JsonDocument.Parse(fullJson);
                        var card = fullDoc.RootElement;

                        var apiName = card.GetProperty("name").GetString() ?? "";
                        var apiNum = card.GetProperty("localId").GetString() ?? "";
                        var apiHp = card.TryGetProperty("hp", out var hp) && hp.ValueKind == JsonValueKind.String
                            ? hp.GetString() ?? "" : "-";

                        string apiSetName = "";
                        if (card.TryGetProperty("set", out var set) && set.ValueKind == JsonValueKind.Object)
                            apiSetName = set.GetProperty("name").GetString() ?? "";

                        int score = 0;
                        if (apiName == cardName) score += 15;
                        if (apiName.StartsWith(cardName, StringComparison.OrdinalIgnoreCase)) score += 5;

                        lock (lockObj)
                        {
                            results.Add(new CardResult
                            {
                                CardId = cardId,
                                Name = apiName,
                                Number = apiNum,
                                SetName = apiSetName,
                                Hp = apiHp,
                                Score = score,
                            });
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception)
                    {
                        // Log error in real implementation
                    }
                    finally
                    {
                        sem.Release();
                    }
                });

                await Task.WhenAll(tasks).WaitAsync(ct);

                results = results.OrderByDescending(r => r.Score).ThenBy(r => r.Name).ToList();
                return results;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // Log error in real implementation
                return results;
            }
        }
    }
}