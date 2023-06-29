using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace ConferenceBooking
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Login", id = UrlParameter.Optional }
            );
            routes.MapRoute(
    name: "AdminPage",
    url: "Home/adminpage",
    defaults: new { controller = "Home", action = "adminpage" }
);
            routes.MapRoute(
                            name: "GetRoomName",
                            url: "Home/GetRoomName/{roomId}",
                            defaults: new { controller = "Home", action = "GetRoomName" }
                        );
        }
    }
}
