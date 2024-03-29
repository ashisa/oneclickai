using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace oneclickai
{
    public static class analyzeimage
    {
        // Specify the features to return
        private static readonly List<VisualFeatureTypes> features =
            new List<VisualFeatureTypes>()
        {
            VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
            VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
            VisualFeatureTypes.Tags, VisualFeatureTypes.Brands, VisualFeatureTypes.Objects
        };

        [FunctionName("analyzeimage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string cognitive_service_key = Environment.GetEnvironmentVariable("cognitive_service_key");
            string cognitive_service_endpoint = Environment.GetEnvironmentVariable("cognitive_service_endpoint");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string imageURL = data.imageurl;
            //imageURL = "https://www.thehansindia.com/assets/9583_rahul-modi.jpg";

            dynamic result = new JObject();

            var credentials = new ApiKeyServiceClientCredentials(cognitive_service_key);

            ComputerVisionClient computerVision = new ComputerVisionClient(credentials,
                new System.Net.Http.DelegatingHandler[] { });

            // Specify the Azure region
            computerVision.Endpoint = cognitive_service_endpoint;

            // Analyzing image from remote URL
            if (!Uri.IsWellFormedUriString(imageURL, UriKind.Absolute))
            {
                log.LogError(
                    "\nInvalid remoteImageUrl:\n{0} \n", imageURL);
                log.LogInformation("invalid image URL provided.");
            }
            else
            {
                ImageAnalysis analysis = new ImageAnalysis();
                try
                {
                    analysis = await computerVision.AnalyzeImageAsync(imageURL, features);

                    // Getting caption
                    result.caption = "";
                    if (analysis.Description.Captions.Count != 0)
                    {
                        result.caption = analysis.Description.Captions[0].Text;
                    }

                    // Getting faces
                    dynamic faces = new JArray();
                    dynamic celebrities = new JArray();
                    if (analysis.Faces.Count != 0)
                    {
                        foreach (var face in analysis.Faces)
                        {
                            dynamic faceObject = new JObject();
                            faceObject.rectangle = $"({face.FaceRectangle.Left.ToString()}, " +
                                $"{face.FaceRectangle.Top.ToString()}, " +
                                $"{face.FaceRectangle.Height.ToString()}, " +
                                $"{face.FaceRectangle.Width.ToString()})";
                            faceObject.age = face.Age;
                            faceObject.gender = face.Gender.ToString();
                            faces.Add(faceObject);
                        }

                        var celebRecognition = await computerVision.AnalyzeImageByDomainAsync("celebrities", imageURL);
                        dynamic celebResult = JsonConvert.DeserializeObject(celebRecognition.Result.ToString());
                        if (celebResult.celebrities.Count > 1)
                        {
                            foreach (var celeb in celebResult.celebrities)
                            {
                                celebrities.Add(celeb);
                            }
                        }
                    }
                    result.faces = faces;
                    result.celebrities = celebrities;

                    // Getting categories
                    dynamic categories = new JArray();
                    if (analysis.Categories.Count != 0)
                    {
                        foreach (var category in analysis.Categories)
                        {
                            categories.Add(category.Name);
                        }
                    }
                    result.categories = categories;

                    // Getting brands
                    dynamic brands = new JArray();
                    if (analysis.Brands.Count != 0)
                    {
                        foreach (var brand in analysis.Brands)
                        {
                            brands.Add(brand.Name);
                        }
                    }
                    result.brands = brands;

                    // Getting objects
                    dynamic objects = new JArray();
                    if (analysis.Objects.Count != 0)
                    {
                        foreach (var objectItem in analysis.Objects)
                        {
                            dynamic objObject = new JObject();
                            objObject.name = objectItem.ObjectProperty;
                            objObject.rectangle = $"({objectItem.Rectangle.X.ToString()}, " +
                                $"{objectItem.Rectangle.Y.ToString()}, " +
                                $"{objectItem.Rectangle.H.ToString()}, " +
                                $"{objectItem.Rectangle.W.ToString()})";
                            objects.Add(objObject);
                        }
                    }
                    result.objects = objects;

                    // Getting tags
                    dynamic tags = new JArray();
                    if (analysis.Tags.Count != 0)
                    {
                        foreach (var tag in analysis.Tags)
                        {
                            tags.Add(tag.Name);
                        }
                    }
                    result.tags = tags;
                }
                catch (Exception ex)
                {
                    string exception = ex.Message;
                }
            }

            return imageURL != null
                ? (ActionResult)new OkObjectResult($"{result.ToString()}")
                : new BadRequestObjectResult("{ \"error\": \"Please pass a valid image URL in the request\"");
        }
    }
}
