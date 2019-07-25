using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oneclickai
{
    public static class textinsights
    {
        [FunctionName("textinsights")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string textAnalyticsAPIKey = Environment.GetEnvironmentVariable("text_analytics_api_key");
            string textAnalyticsEndpoint = Environment.GetEnvironmentVariable("text_analytics_endpoint");
            textAnalyticsAPIKey = "8c88c59bed99405ca857cc531a1fd105";
            textAnalyticsEndpoint = "https://centralindia.api.cognitive.microsoft.com";

            int SentencesToSummarize = 3;

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string inputText = data.text;

            var credentials = new ApiKeyServiceClientCredentials(textAnalyticsAPIKey);
            var client = new TextAnalyticsClient(credentials)
            {
                Endpoint = textAnalyticsEndpoint
            };

            dynamic result = new JObject();

            //Detecting language first
            var inputDocuments = new LanguageBatchInput(
                    new List<LanguageInput>
                        {
                    new LanguageInput(id: "1", text: inputText)
                        });

            var langResults = await client.DetectLanguageAsync(false, inputDocuments);
            string inputLanguage = null;
            foreach (var document in langResults.Documents)
            {
                inputLanguage = document.DetectedLanguages[0].Iso6391Name;
            }

            result.language = inputLanguage;
            log.LogInformation($"{result.ToString()}");

            //Detecting sentiment of the input text
            var inputDocuments2 = new MultiLanguageBatchInput(
            new List<MultiLanguageInput>
            {
            new MultiLanguageInput(inputLanguage, "1", inputText)
            });

            var sentimentResult = await client.SentimentAsync(false, inputDocuments2);
            double? sentimentScore = 0;
            foreach (var document in sentimentResult.Documents)
            {
                sentimentScore = document.Score;
            }

            result.sentimentScore = sentimentScore;
            log.LogInformation($"{result.ToString()}");

            //Detecting entities in the text
            var entitiesResult = await client.EntitiesAsync(false, inputDocuments2);
            JArray entities = new JArray();
            foreach (var document in entitiesResult.Documents)
            {
                dynamic entityObject = new JObject();
                foreach (var entity in document.Entities)
                {
                    entityObject.name = entity.Name;
                    entityObject.type = entity.Type;
                    entityObject.subtype = entity.SubType;
                    foreach (var match in entity.Matches)
                    {
                        entityObject.offset = match.Offset;
                        entityObject.length = match.Length;
                        entityObject.score = match.EntityTypeScore;
                        //log.LogInformation($"\t\t\tOffset: {match.Offset},\tLength: {match.Length},\tScore: {match.EntityTypeScore:F3}");
                    }
                    entities.Add(entityObject);
                }
            }
            result.entities = entities;
            log.LogInformation($"{result.ToString()}");

            //Detecting keyphrases
            var kpResults = await client.KeyPhrasesAsync(false, inputDocuments2);
            JArray keyPhrases = new JArray();
            var Phrases = new List<string>();

            // Printing keyphrases
            foreach (var document in kpResults.Documents)
            {
                foreach (string keyphrase in document.KeyPhrases)
                {
                    keyPhrases.Add(keyphrase);
                    Phrases.Add(keyphrase);
                }
            }
            result.keyphrases = keyPhrases;

            //Generating text summary
            String[] sentences = inputText.Split('!', '.', '?');

            List<Match> matchList = new List<Match>();
            int counter = 0;
            // Take the 10 best words
            var topPhrases = Phrases.Take(10);
            foreach (var sentence in sentences)
            {
                double count = 0;

                Match match = new Match();
                foreach (var phrase in topPhrases)
                {
                    if ((sentence.ToLower().IndexOf(phrase) > -1) &&
                        (sentence.Length > 20) && (WordCount(sentence) >= 3))
                        count++; ;
                }

                if (count > 0)
                    matchList.Add(new Match { sentence = counter, total = count });
                counter++;
            }

            var MatchList = matchList.OrderByDescending(y => y.total).Take(SentencesToSummarize).OrderBy(x => x.sentence).ToList();
            StringBuilder summary = new StringBuilder();
            List<string> SentenceList = new List<string>();
            int sentenceCount = 0;
            for (int i = 0; i < MatchList.Count; i++)
            {
                summary.Append(sentences[MatchList[i].sentence] + ".");
                sentenceCount++;
            }
            // If there are no sentences found, just take the first three
            if (sentenceCount == 0)
            {
                for (int i = 0; i < Math.Min(SentencesToSummarize, sentences.Count()); i++)
                {
                    summary.Append(sentences[MatchList[i].sentence] + ".");
                }
            }

            result.summary = summary.ToString();
            log.LogInformation($"{result.ToString()}");

            return inputText != null
                ? (ActionResult)new OkObjectResult($"{result.ToString()}")
                : new BadRequestObjectResult("{ \"error\": \"Please pass the text input for the text analytics operations\"");
        }
        public static int WordCount(string text)
        {
            // Calculate total word count in text
            int wordCount = 0, index = 0;

            while (index < text.Length)
            {
                // check if current char is part of a word
                while (index < text.Length && !char.IsWhiteSpace(text[index]))
                    index++;

                wordCount++;

                // skip whitespace until next word
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;
            }

            return wordCount;
        }
    }

    public class Match
    {
        public int sentence { get; set; }
        public double total { get; set; }
    }
}
