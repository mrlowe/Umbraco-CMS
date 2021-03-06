using System;
using System.IO;
using System.Web.Routing;
using System.Xml;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.ObjectResolution;
using Umbraco.Tests.Stubs;
using Umbraco.Web;
using Umbraco.Web.Routing;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.cache;
using umbraco.cms.businesslogic.template;

namespace Umbraco.Tests.TestHelpers
{
	[TestFixture, RequiresSTA]
	public abstract class BaseWebTest
	{

		[SetUp]
		public virtual void Initialize()
		{
            SettingsForTests.Reset();

			TestHelper.SetupLog4NetForTests();   

			AppDomain.CurrentDomain.SetData("DataDirectory", TestHelper.CurrentAssemblyDirectory);

			if (RequiresDbSetup)
				TestHelper.InitializeDatabase();
			Resolution.Freeze();
			ApplicationContext = new ApplicationContext() { IsReady = true };
            //init the singleton too!
            ApplicationContext.Current = ApplicationContext;
			//we need to clear out all currently created template files
			var masterPages = new DirectoryInfo(IOHelper.MapPath(SystemDirectories.Masterpages));
			masterPages.GetFiles().ForEach(x => x.Delete());
			var mvcViews = new DirectoryInfo(IOHelper.MapPath(SystemDirectories.MvcViews));
			mvcViews.GetFiles().ForEach(x => x.Delete());
		}

		[TearDown]
		public virtual void TearDown()
		{
			//reset the app context
			ApplicationContext.ApplicationCache.ClearAllCache();
			ApplicationContext.Current = null;
			Resolution.IsFrozen = false;
			if (RequiresDbSetup)
				TestHelper.ClearDatabase();

            AppDomain.CurrentDomain.SetData("DataDirectory", null);

			Cache.ClearAllCache();

			SettingsForTests.Reset();
		}	

		/// <summary>
		/// By default this unit test will create and initialize an umbraco database
		/// </summary>
		protected virtual bool RequiresDbSetup
		{
			get { return true; }
		}

		protected FakeHttpContextFactory GetHttpContextFactory(string url, RouteData routeData = null)
		{
			var factory = routeData != null
			              	? new FakeHttpContextFactory(url, routeData)
			              	: new FakeHttpContextFactory(url);


			//set the state helper
			StateHelper.HttpContext = factory.HttpContext;

			return factory;
		}

		protected ApplicationContext ApplicationContext { get; private set; }

		internal virtual IRoutesCache GetRoutesCache()
		{
			return new FakeRoutesCache();
		}

		protected UmbracoContext GetUmbracoContext(string url, int templateId, RouteData routeData = null)
		{
			var ctx = new UmbracoContext(
				GetHttpContextFactory(url, routeData).HttpContext,
				ApplicationContext,
				GetRoutesCache());
			SetupUmbracoContextForTest(ctx, templateId);
			return ctx;
		}

		protected virtual string GetXmlContent(int templateId)
		{
			return @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE root[ 
<!ELEMENT Home ANY>
<!ATTLIST Home id ID #REQUIRED>
<!ELEMENT CustomDocument ANY>
<!ATTLIST CustomDocument id ID #REQUIRED>
]>
<root id=""-1"">
	<Home id=""1046"" parentID=""-1"" level=""1"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-06-12T14:13:17"" updateDate=""2012-07-20T18:50:43"" nodeName=""Home"" urlName=""home"" writerName=""admin"" creatorName=""admin"" path=""-1,1046"" isDoc="""">
		<content><![CDATA[]]></content>
		<umbracoUrlAlias><![CDATA[this/is/my/alias, anotheralias]]></umbracoUrlAlias>
		<umbracoNaviHide>1</umbracoNaviHide>
		<Home id=""1173"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-20T18:06:45"" updateDate=""2012-07-20T19:07:31"" nodeName=""Sub1"" urlName=""sub1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173"" isDoc="""">
			<content><![CDATA[<div>This is some content</div>]]></content>
			<umbracoUrlAlias><![CDATA[page2/alias, 2ndpagealias]]></umbracoUrlAlias>			
			<Home id=""1174"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-20T18:07:54"" updateDate=""2012-07-20T19:10:27"" nodeName=""Sub2"" urlName=""sub2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1174"" isDoc="""">
				<content><![CDATA[]]></content>
				<umbracoUrlAlias><![CDATA[only/one/alias]]></umbracoUrlAlias>
				<creatorName><![CDATA[Custom data with same property name as the member name]]></creatorName>
			</Home>
			<Home id=""1176"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""3"" createDate=""2012-07-20T18:08:08"" updateDate=""2012-07-20T19:10:52"" nodeName=""Sub 3"" urlName=""sub-3"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1176"" isDoc="""">
				<content><![CDATA[]]></content>
			</Home>
			<CustomDocument id=""1177"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""4"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""custom sub 1"" urlName=""custom-sub-1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1177"" isDoc="""" />
			<CustomDocument id=""1178"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""4"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-16T14:23:35"" nodeName=""custom sub 2"" urlName=""custom-sub-2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1178"" isDoc="""" />
		</Home>
		<Home id=""1175"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""3"" createDate=""2012-07-20T18:08:01"" updateDate=""2012-07-20T18:49:32"" nodeName=""Sub 2"" urlName=""sub-2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1175"" isDoc=""""><content><![CDATA[]]></content>
		</Home>
	</Home>
	<CustomDocument id=""1172"" parentID=""-1"" level=""1"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""Test"" urlName=""test-page"" writerName=""admin"" creatorName=""admin"" path=""-1,1172"" isDoc="""" />
</root>";
		}

		/// <summary>
		/// Initlializes the UmbracoContext with specific XML
		/// </summary>
		/// <param name="umbracoContext"></param>
		/// <param name="templateId"></param>
		protected void SetupUmbracoContextForTest(UmbracoContext umbracoContext, int templateId)
		{
			umbracoContext.GetXmlDelegate = () =>
				{
					var xDoc = new XmlDocument();

					//create a custom xml structure to return

					xDoc.LoadXml(GetXmlContent(templateId));
					//return the custom x doc
					return xDoc;
				};
		}
	}
}