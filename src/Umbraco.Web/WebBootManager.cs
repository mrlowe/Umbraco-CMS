﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Dictionary;
using Umbraco.Core.Dynamics;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.Dictionary;
using Umbraco.Web.Media;
using Umbraco.Web.Media.ThumbnailProviders;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;
using Umbraco.Web.PropertyEditors;
using Umbraco.Web.Routing;
using umbraco.businesslogic;
using umbraco.cms.businesslogic;


namespace Umbraco.Web
{
    /// <summary>
    /// A bootstrapper for the Umbraco application which initializes all objects including the Web portion of the application 
    /// </summary>
    internal class WebBootManager : CoreBootManager
    {
        private readonly bool _isForTesting;
        private readonly UmbracoApplication _umbracoApplication;

        public WebBootManager(UmbracoApplication umbracoApplication)
            : this(umbracoApplication, false)
        {
        }

        /// <summary>
        /// Constructor for unit tests, ensures some resolvers are not initialized
        /// </summary>
        /// <param name="umbracoApplication"></param>
        /// <param name="isForTesting"></param>
        internal WebBootManager(UmbracoApplication umbracoApplication, bool isForTesting)
        {
            _isForTesting = isForTesting;
            _umbracoApplication = umbracoApplication;
            if (umbracoApplication == null) throw new ArgumentNullException("umbracoApplication");
        }

        [Obsolete("This method was never supposed to be here and will be removed in v6.0.0")]
        public void Boot()
        {
            InitializeResolvers();
        }

        /// <summary>
        /// Initialize objects before anything during the boot cycle happens
        /// </summary>
        /// <returns></returns>
        public override IBootManager Initialize()
        {            
            base.Initialize();

            // Backwards compatibility - set the path and URL type for ClientDependency 1.5.1 [LK]
            ClientDependency.Core.CompositeFiles.Providers.XmlFileMapper.FileMapVirtualFolder = "~/App_Data/TEMP/ClientDependency";
            ClientDependency.Core.CompositeFiles.Providers.BaseCompositeFileProcessingProvider.UrlTypeDefault = ClientDependency.Core.CompositeFiles.Providers.CompositeUrlType.Base64QueryStrings;

            //set master controller factory
            ControllerBuilder.Current.SetControllerFactory(
                new MasterControllerFactory(FilteredControllerFactoriesResolver.Current));

            //set the render view engine
            ViewEngines.Engines.Add(new RenderViewEngine());
            //set the plugin view engine
            ViewEngines.Engines.Add(new PluginViewEngine());

            //set model binder
            ModelBinders.Binders.Add(new KeyValuePair<Type, IModelBinder>(typeof(RenderModel), new RenderModelBinder()));


            //find and initialize the application startup handlers, we need to initialize this resolver here because
            //it is a special resolver where they need to be instantiated first before any other resolvers in order to bind to 
            //events and to call their events during bootup.
            //ApplicationStartupHandler.RegisterHandlers();
            //... and set the special flag to let us resolve before frozen resolution
            ApplicationEventsResolver.Current = new ApplicationEventsResolver(
                PluginManager.Current.ResolveApplicationStartupHandlers())
            {
                CanResolveBeforeFrozen = true
            };
            //add the internal types since we don't want to mark these public
            ApplicationEventsResolver.Current.AddType<CacheHelperExtensions.CacheHelperApplicationEventListener>();
            ApplicationEventsResolver.Current.AddType<LegacyScheduledTasks>();

            //now we need to call the initialize methods
            ApplicationEventsResolver.Current.ApplicationEventHandlers
                .ForEach(x => x.OnApplicationInitialized(_umbracoApplication, ApplicationContext));

            return this;
        }

        /// <summary>
        /// Override this method in order to ensure that the UmbracoContext is also created, this can only be 
        /// created after resolution is frozen!
        /// </summary>
        protected override void FreezeResolution()
        {
            base.FreezeResolution();

            //before we do anything, we'll ensure the umbraco context
            //see: http://issues.umbraco.org/issue/U4-1717
            UmbracoContext.EnsureContext(new HttpContextWrapper(_umbracoApplication.Context), ApplicationContext);
        }

        /// <summary>
        /// Ensure that the OnApplicationStarting methods of the IApplicationEvents are called
        /// </summary>
        /// <param name="afterStartup"></param>
        /// <returns></returns>
        public override IBootManager Startup(Action<ApplicationContext> afterStartup)
        {
            base.Startup(afterStartup);

            //call OnApplicationStarting of each application events handler
            ApplicationEventsResolver.Current.ApplicationEventHandlers
                .ForEach(x => x.OnApplicationStarting(_umbracoApplication, ApplicationContext));

            return this;
        }

        /// <summary>
        /// Ensure that the OnApplicationStarted methods of the IApplicationEvents are called
        /// </summary>
        /// <param name="afterComplete"></param>
        /// <returns></returns>
        public override IBootManager Complete(Action<ApplicationContext> afterComplete)
        {
            //set routes
            CreateRoutes();

            base.Complete(afterComplete);

            //call OnApplicationStarting of each application events handler
            ApplicationEventsResolver.Current.ApplicationEventHandlers
                .ForEach(x => x.OnApplicationStarted(_umbracoApplication, ApplicationContext));

            //Now, startup all of our legacy startup handler
            ApplicationEventsResolver.Current.InstantiateLegacyStartupHanlders();

            // we're ready to serve content!
            ApplicationContext.IsReady = true;

            return this;
        }

        /// <summary>
        /// Creates the routes
        /// </summary>
        protected internal void CreateRoutes()
        {
            var umbracoPath = GlobalSettings.UmbracoMvcArea;

            //Create the front-end route
            var defaultRoute = RouteTable.Routes.MapRoute(
                "Umbraco_default",
                umbracoPath + "/RenderMvc/{action}/{id}",
                new { controller = "RenderMvc", action = "Index", id = UrlParameter.Optional }
                );
            defaultRoute.RouteHandler = new RenderRouteHandler(ControllerBuilder.Current.GetControllerFactory());

            //Create the install routes
            var installPackageRoute = RouteTable.Routes.MapRoute(
                "Umbraco_install_packages",
                "Install/PackageInstaller/{action}/{id}",
                new { controller = "InstallPackage", action = "Index", id = UrlParameter.Optional }
                );
            installPackageRoute.DataTokens.Add("area", umbracoPath);

            //we need to find the surface controllers and route them
            var surfaceControllers = SurfaceControllerResolver.Current.RegisteredSurfaceControllers.ToArray();

            //local surface controllers do not contain the attribute 			
            var localSurfaceControlleres = surfaceControllers.Where(x => PluginController.GetMetadata(x).AreaName.IsNullOrWhiteSpace());
            foreach (var s in localSurfaceControlleres)
            {
                var meta = PluginController.GetMetadata(s);
                var route = RouteTable.Routes.MapRoute(
                    string.Format("umbraco-{0}-{1}", "surface", meta.ControllerName),
                    umbracoPath + "/Surface/" + meta.ControllerName + "/{action}/{id}",//url to match
                    new { controller = meta.ControllerName, action = "Index", id = UrlParameter.Optional },
                    new[] { meta.ControllerNamespace }); //only match this namespace
                route.DataTokens.Add("umbraco", "surface"); //ensure the umbraco token is set
            }

            //need to get the plugin controllers that are unique to each area (group by)
            //TODO: One day when we have more plugin controllers, we will need to do a group by on ALL of them to pass into the ctor of PluginControllerArea
            var pluginSurfaceControlleres = surfaceControllers.Where(x => !PluginController.GetMetadata(x).AreaName.IsNullOrWhiteSpace());
            var groupedAreas = pluginSurfaceControlleres.GroupBy(controller => PluginController.GetMetadata(controller).AreaName);
            //loop through each area defined amongst the controllers
            foreach (var g in groupedAreas)
            {
                //create an area for the controllers (this will throw an exception if all controllers are not in the same area)
                var pluginControllerArea = new PluginControllerArea(g.Select(PluginController.GetMetadata));
                //register it
                RouteTable.Routes.RegisterArea(pluginControllerArea);
            }
        }



        /// <summary>
        /// Initializes all web based and core resolves 
        /// </summary>
        protected override void InitializeResolvers()
        {
            base.InitializeResolvers();

            //TODO: This needs to be removed in future versions (i.e. 6.0 when the PublishedContentHelper can access the business logic)
            // see the TODO noted in the PublishedContentHelper.
            PublishedContentHelper.GetDataTypeCallback = ContentType.GetDataType;

            SurfaceControllerResolver.Current = new SurfaceControllerResolver(
                PluginManager.Current.ResolveSurfaceControllers());

            //the base creates the PropertyEditorValueConvertersResolver but we want to modify it in the web app and replace
            //the TinyMcePropertyEditorValueConverter with the RteMacroRenderingPropertyEditorValueConverter
            PropertyEditorValueConvertersResolver.Current.RemoveType<TinyMcePropertyEditorValueConverter>();
            PropertyEditorValueConvertersResolver.Current.AddType<RteMacroRenderingPropertyEditorValueConverter>();

            PublishedContentStoreResolver.Current = new PublishedContentStoreResolver(new DefaultPublishedContentStore());
            PublishedMediaStoreResolver.Current = new PublishedMediaStoreResolver(new DefaultPublishedMediaStore());

            FilteredControllerFactoriesResolver.Current = new FilteredControllerFactoriesResolver(
                //add all known factories, devs can then modify this list on application startup either by binding to events
                //or in their own global.asax
                new[]
					{
						typeof (RenderControllerFactory)
					});

            // the legacy 404 will run from within LookupByNotFoundHandlers below
            // so for the time being there is no last chance lookup
			LastChanceLookupResolver.Current = new LastChanceLookupResolver();

            DocumentLookupsResolver.Current = new DocumentLookupsResolver(
                //add all known resolvers in the correct order, devs can then modify this list on application startup either by binding to events
                //or in their own global.asax
                new[]
					{
						typeof (LookupByPageIdQuery),
						typeof (LookupByNiceUrl),
						typeof (LookupByIdPath),
                        // these will be handled by LookupByNotFoundHandlers
                        // so they can be enabled/disabled even though resolvers are not public yet
						//typeof (LookupByNiceUrlAndTemplate),
						//typeof (LookupByProfile),
						//typeof (LookupByAlias),
                        typeof (LookupByNotFoundHandlers)
					});

            RoutesCacheResolver.Current = new RoutesCacheResolver(new DefaultRoutesCache(_isForTesting == false));

            ThumbnailProvidersResolver.Current = new ThumbnailProvidersResolver(
                PluginManager.Current.ResolveThumbnailProviders());

            ImageUrlProviderResolver.Current = new ImageUrlProviderResolver(
                PluginManager.Current.ResolveImageUrlProviders());

            CultureDictionaryFactoryResolver.Current = new CultureDictionaryFactoryResolver(
                new DefaultCultureDictionaryFactory());

        }

    }
}
