using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.Reflection;
using System.Diagnostics;
using Umbraco.Core;
using umbraco.IO;

namespace umbraco.dialogs
{
	/// <summary>
	/// Summary description for about.
	/// </summary>
	public partial class about : BasePages.UmbracoEnsuredPage
	{

		protected void Page_Load(object sender, System.EventArgs e)
		{
			// Put user code to initialize the page here
			version.Text = GlobalSettings.CurrentVersion;
            thisYear.Text = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
            
			// umbraco.dll version
            var assemblyVersion = new AssemblyName(typeof(ActionsResolver).Assembly.FullName).Version.ToString();
            version.Text += string.Format(" (Assembly version: {0})", assemblyVersion);
		}

		#region Web Form Designer generated code
		override protected void OnInit(EventArgs e)
		{
			//
			// CODEGEN: This call is required by the ASP.NET Web Form Designer.
			//
			InitializeComponent();
			base.OnInit(e);
		}
		
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{    

		}
		#endregion
	}
}
