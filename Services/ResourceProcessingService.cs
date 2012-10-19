using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Orchard.FileSystems.Media;
using Piedone.Combinator.Extensions;
using Piedone.Combinator.Models;

using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.Utility.Extensions;
using Orchard.Mvc.Extensions; 

namespace Piedone.Combinator.Services
{
    public class ResourceProcessingService : IResourceProcessingService {
        private readonly IStorageProvider _storageProvider;
        private readonly IResourceFileService _resourceFileService;
        private readonly IMinificationService _minificationService;
        private static readonly bool AzureMode = true;
        
        private const string _rootPath = "Combinator/";
        private string _publicUrlExample = "";
        private string PublicUrlExample {
            get {
                if (string.IsNullOrWhiteSpace(_publicUrlExample)) {
                    var path = _rootPath + "sample.txt";
                    try {
                        var file = _storageProvider.CreateFile(path);
                    }
                    catch (Exception e) { 
                    //    exists = e.Message.IndexOf("exists", StringComparison.OrdinalIgnoreCase)>=0; 
                    }

                    _publicUrlExample = _storageProvider.GetPublicUrl(path); 
                }
                return _publicUrlExample; 
            }
        }

        // can cache this if needed:
        private bool IsMediaStorageUsingAbsoluteUris {
            get {
                return Uri.IsWellFormedUriString(PublicUrlExample, UriKind.Absolute);
            }
        }

        public ResourceProcessingService(
            IResourceFileService resourceFileService,
            IMinificationService minificationService,
            IStorageProvider storageProvider)
        {
            _resourceFileService = resourceFileService;
            _minificationService = minificationService;
            _storageProvider = storageProvider; 
            
        }

        public void ProcessResource(CombinatorResource resource, StringBuilder combinedContent, ICombinatorSettings settings)
        {
            if (!resource.IsCdnResource || settings.CombineCDNResources)
            {
                var absoluteUrlString = resource.AbsoluteUrl.ToString();

                if (!resource.IsCdnResource)
                {
                    resource.Content = _resourceFileService.GetLocalResourceContent(resource);
                }
                else if (settings.CombineCDNResources)
                {
                    resource.Content = _resourceFileService.GetRemoteResourceContent(resource);
                }

                if (String.IsNullOrEmpty(resource.Content)) return;

                if (settings.MinifyResources && (settings.MinificationExcludeFilter == null || !settings.MinificationExcludeFilter.IsMatch(absoluteUrlString)))
                {
                    MinifyResourceContent(resource);
                    if (String.IsNullOrEmpty(resource.Content)) return;
                }

                // Better to do after minification, as then urls commented out are removed
                if (resource.Type == ResourceType.Style)
                {
                    AdjustRelativePaths(resource);

                    if (settings.EmbedCssImages && (settings.EmbedCssImagesStylesheetExcludeFilter == null || !settings.EmbedCssImagesStylesheetExcludeFilter.IsMatch(absoluteUrlString)))
                    {
                        EmbedImages(resource, settings.EmbeddedImagesMaxSizeKB);
                    }
                }

                combinedContent.Append(resource.Content);
            }
            else
            {
                resource.IsOriginal = true;
            }
        }

        private void EmbedImages(CombinatorResource resource, int maxSizeKB)
        {
            ProcessUrlSettings(resource,
                (match) =>
                {
                    var url = match.Groups[1].Value;
                    var extension = Path.GetExtension(url).ToLowerInvariant();

                    // This is a dumb check but otherwise we'd have to inspect the file thoroughly
                    if (!String.IsNullOrEmpty(extension) && ".jpg .jpeg .png .gif .tiff .bmp".Contains(extension))
                    {
                        var imageData = _resourceFileService.GetImageBase64Data(MakeInlineUri(resource, url), maxSizeKB);

                        if (!String.IsNullOrEmpty(imageData))
                        {
                            var dataUrl =
                            "data:image/"
                                + Path.GetExtension(url).Replace(".", "")
                                + ";base64,"
                                + imageData;

                            return "url(\"" + dataUrl + "\")";
                        }
                    }

                    return match.Groups[0].Value;
                });
        }

        private void AdjustRelativePaths(CombinatorResource resource)
        {
            var urlHelper = GetUrlHelper(); 
            ProcessUrlSettings(resource,
                (match) =>
                {
                    var url = match.Groups[1].ToString();

                    var uri = MakeInlineUri(resource, url);

                    // Remote paths are preserved as full urls, local paths become uniformed relative ones.
                    string uriString = "";
                    if (IsMediaStorageUsingAbsoluteUris && !resource.IsCdnResource) {
                        uriString = urlHelper.MakeAbsolute(urlHelper.Content("~" + uri.GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped) ));
                    }
                    else if (resource.IsCdnResource || resource.AbsoluteUrl.Host != uri.Host) uriString = uri.ToStringWithoutScheme();
                    else uriString = uri.PathAndQuery;

                    return "url(\"" + uriString + "\")";
                });
        }

        private static UrlHelper GetUrlHelper() {
            var httpContext = HttpContext.Current;

            if (httpContext == null) {
                var request = new HttpRequest("/", "http://example.com", "");
                var response = new HttpResponse(new StringWriter());
                httpContext = new HttpContext(request, response);
            }

            var httpContextBase = new HttpContextWrapper(httpContext);
            var routeData = new RouteData();
            var requestContext = new RequestContext(httpContextBase, routeData);

            return new UrlHelper(requestContext);
        }

        private static void ProcessUrlSettings(CombinatorResource resource, MatchEvaluator evaluator)
        {
            string content = resource.Content;

            content = Regex.Replace(
                                    content,
                                    "url\\(['|\"]?(.+?)['|\"]?\\)",
                                    evaluator,
                                    RegexOptions.IgnoreCase);

            resource.Content = content;
        }

        private static Uri MakeInlineUri(CombinatorResource resource, string url)
        {
            return Uri.IsWellFormedUriString(url, UriKind.Absolute) ? new Uri(url) : new Uri(resource.AbsoluteUrl, url);
        }

        private void MinifyResourceContent(CombinatorResource resource)
        {
            if (resource.Type == ResourceType.Style)
            {
                resource.Content = _minificationService.MinifyCss(resource.Content);
            }
            else if (resource.Type == ResourceType.JavaScript)
            {
                resource.Content = _minificationService.MinifyJavaScript(resource.Content);
            }
        }
    }
}