using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.Utility.Extensions;
using Orchard.Mvc.Extensions; 

namespace Piedone.Combinator.Extensions
{
    // Use the one from Helpful Libraries once there will be more new things to use form it.
    public static class UriExtensions
    {
        public static string ToStringWithoutScheme(this Uri uri)
        {
            if (!uri.IsAbsoluteUri) return uri.ToString();
            //if (uri.IsDefaultPort) {
            //    return "//" + uri.GetComponents(UriComponents.Host | UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped);
            //}
            //else {
            //    return "//" + uri.GetComponents(UriComponents.HostAndPort | UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped);
            //}
            var urlHelper = GetUrlHelper();
            return urlHelper.MakeAbsolute(uri.ToString()); 
            
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
    }
}