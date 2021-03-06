using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using Umbraco.Core.IO;
using umbraco.BusinessLogic.Actions;
using umbraco.uicontrols.DatePicker;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.web;
using umbraco.presentation;
using System.Linq;
using Image = System.Web.UI.WebControls.Image;
using Umbraco.Core;

namespace umbraco.cms.presentation
{
    public partial class editContent : BasePages.UmbracoEnsuredPage
    {
        protected uicontrols.TabView TabView1;
        protected TextBox documentName;
        private Document _document;
        private bool _documentHasPublishedVersion = false;
        protected Literal jsIds;
        private readonly LiteralControl _dp = new LiteralControl();
        private readonly DateTimePicker _dpRelease = new DateTimePicker();
        private readonly DateTimePicker _dpExpire = new DateTimePicker();

        private controls.ContentControl _cControl;

        private readonly DropDownList _ddlDefaultTemplate = new DropDownList();

        private readonly uicontrols.Pane _publishProps = new uicontrols.Pane();
        private readonly uicontrols.Pane _linkProps = new uicontrols.Pane();

        private readonly Button _unPublish = new Button();
        private readonly Literal _littPublishStatus = new Literal();

        private controls.ContentControl.publishModes _canPublish = controls.ContentControl.publishModes.Publish;

        private int? _contentId = null;

        override protected void OnInit(EventArgs e)
        {
            base.OnInit(e);

            //validate!
            int id;
            if (int.TryParse(Request.QueryString["id"], out id) == false)
            {
                //if this is invalid show an error
                this.DisplayFatalError("Invalid query string");
                return;
            }
            _contentId = id;


            _unPublish.Click += UnPublishDo;

            //_document = new cms.businesslogic.web.Document(int.Parse(Request.QueryString["id"]));
            _document = new Document(true, id);

            //check if the doc exists
            if (string.IsNullOrEmpty(_document.Path))
            {
                //if this is invalid show an error
                this.DisplayFatalError("No document found with id " + _contentId);
                //reset the content id to null so processing doesn't continue on OnLoad
                _contentId = null;
                return;
            }

            // we need to check if there's a published version of this document
            _documentHasPublishedVersion = _document.HasPublishedVersion();

            // Check publishing permissions
            if (base.getUser().GetPermissions(_document.Path).Contains(ActionPublish.Instance.Letter.ToString()) == false)
                _canPublish = controls.ContentControl.publishModes.SendToPublish;
            _cControl = new controls.ContentControl(_document, _canPublish, "TabView1");

            _cControl.ID = "TabView1";

            _cControl.Width = Unit.Pixel(666);
            _cControl.Height = Unit.Pixel(666);

            // Add preview button

            foreach (uicontrols.TabPage tp in _cControl.GetPanels())
            {
                AddPreviewButton(tp.Menu, _document.Id);
            }

            plc.Controls.Add(_cControl);


            var publishStatus = new PlaceHolder();
            if (_documentHasPublishedVersion)
            {
                _littPublishStatus.Text = ui.Text("content", "lastPublished", base.getUser()) + ": " + _document.VersionDate.ToShortDateString() + " &nbsp; ";

                publishStatus.Controls.Add(_littPublishStatus);
                if (getUser().GetPermissions(_document.Path).IndexOf("U") > -1)
                    _unPublish.Visible = true;
                else
                    _unPublish.Visible = false;
            }
            else
            {
                _littPublishStatus.Text = ui.Text("content", "itemNotPublished", base.getUser());
                publishStatus.Controls.Add(_littPublishStatus);
                _unPublish.Visible = false;
            }

            _unPublish.Text = ui.Text("content", "unPublish", base.getUser());
            _unPublish.ID = "UnPublishButton";
            _unPublish.Attributes.Add("onClick", "if (!confirm('" + ui.Text("defaultdialogs", "confirmSure", base.getUser()) + "')) return false; ");
            publishStatus.Controls.Add(_unPublish);

            _publishProps.addProperty(ui.Text("content", "publishStatus", base.getUser()), publishStatus);

            // Template
            var template = new PlaceHolder();
            var DocumentType = new DocumentType(_document.ContentType.Id);
            _cControl.PropertiesPane.addProperty(ui.Text("documentType"), new LiteralControl(DocumentType.Text));


            //template picker
            _cControl.PropertiesPane.addProperty(ui.Text("template"), template);
            int defaultTemplate;
            if (_document.Template != 0)
                defaultTemplate = _document.Template;
            else
                defaultTemplate = DocumentType.DefaultTemplate;

            if (getUser().UserType.Name == "writer")
            {
                if (defaultTemplate != 0)
                    template.Controls.Add(new LiteralControl(businesslogic.template.Template.GetTemplate(defaultTemplate).Text));
                else
                    template.Controls.Add(new LiteralControl(ui.Text("content", "noDefaultTemplate")));
            }
            else
            {
                _ddlDefaultTemplate.Items.Add(new ListItem(ui.Text("choose") + "...", ""));
                foreach (var t in DocumentType.allowedTemplates)
                {

                    var tTemp = new ListItem(t.Text, t.Id.ToString());
                    if (t.Id == defaultTemplate)
                        tTemp.Selected = true;
                    _ddlDefaultTemplate.Items.Add(tTemp);
                }
                template.Controls.Add(_ddlDefaultTemplate);
            }


            // Editable update date, release date and expire date added by NH 13.12.04
            _dp.ID = "updateDate";
            _dp.Text = _document.UpdateDate.ToShortDateString() + " " + _document.UpdateDate.ToShortTimeString();
            _publishProps.addProperty(ui.Text("content", "updateDate", getUser()), _dp);

            _dpRelease.ID = "releaseDate";
            _dpRelease.DateTime = _document.ReleaseDate;
            _dpRelease.ShowTime = true;
            _publishProps.addProperty(ui.Text("content", "releaseDate", getUser()), _dpRelease);

            _dpExpire.ID = "expireDate";
            _dpExpire.DateTime = _document.ExpireDate;
            _dpExpire.ShowTime = true;
            _publishProps.addProperty(ui.Text("content", "expireDate", getUser()), _dpExpire);

            _cControl.Save += Save;
            _cControl.SaveAndPublish += Publish;
            _cControl.SaveToPublish += SendToPublish;

            // Add panes to property page...
            _cControl.tpProp.Controls.AddAt(1, _publishProps);
            _cControl.tpProp.Controls.AddAt(2, _linkProps);

            // add preview to properties pane too
            AddPreviewButton(_cControl.tpProp.Menu, _document.Id);


        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!_contentId.HasValue)
                return;

            if (!CheckUserValidation())
                return;

            // clear preview cookie
            // zb-00004 #29956 : refactor cookies names & handling
            StateHelper.Cookies.Preview.Clear();

            if (!IsPostBack)
            {

                Log.Add(LogTypes.Open, base.getUser(), _document.Id, "");
                ClientTools.SyncTree(_document.Path, false);
            }


            jsIds.Text = "var umbPageId = " + _document.Id.ToString() + ";\nvar umbVersionId = '" + _document.Version.ToString() + "';\n";

        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            UpdateNiceUrls();
        }

        protected void Save(object sender, EventArgs e)
        {
            // error handling test
            if (Page.IsValid == false)
            {
                foreach (uicontrols.TabPage tp in _cControl.GetPanels())
                {
                    tp.ErrorControl.Visible = true;
                    tp.ErrorHeader = ui.Text("errorHandling", "errorButDataWasSaved");
                    tp.CloseCaption = ui.Text("close");
                }
            }
            else if (Page.IsPostBack)
            {
                // hide validation summaries
                foreach (uicontrols.TabPage tp in _cControl.GetPanels())
                {
                    tp.ErrorControl.Visible = false;
                }
            }
            //Audit trail...
            Log.Add(LogTypes.Save, getUser(), _document.Id, "");

            // Update name if it has not changed and is not empty
            if (_cControl.NameTxt != null && _document.Text != _cControl.NameTxt.Text && !_cControl.NameTxt.Text.IsNullOrWhiteSpace())
            {
                //_refreshTree = true;
                _document.Text = _cControl.NameTxt.Text;
                //newName.Text = _document.Text;
            }


            if (_dpRelease.DateTime > new DateTime(1753, 1, 1) && _dpRelease.DateTime < new DateTime(9999, 12, 31))
                _document.ReleaseDate = _dpRelease.DateTime;
            else
                _document.ReleaseDate = new DateTime(1, 1, 1, 0, 0, 0);
            if (_dpExpire.DateTime > new DateTime(1753, 1, 1) && _dpExpire.DateTime < new DateTime(9999, 12, 31))
                _document.ExpireDate = _dpExpire.DateTime;
            else
                _document.ExpireDate = new DateTime(1, 1, 1, 0, 0, 0);

            // Update default template
            if (_ddlDefaultTemplate.SelectedIndex > 0)
            {
                _document.Template = int.Parse(_ddlDefaultTemplate.SelectedValue);
            }
            else
            {
                if (new DocumentType(_document.ContentType.Id).allowedTemplates.Length == 0)
                {
                    _document.RemoveTemplate();
                }
            }

            // Run Handler				
            BusinessLogic.Actions.Action.RunActionHandlers(_document, ActionUpdate.Instance);
            _document.Save();

            // Update the update date
            _dp.Text = _document.UpdateDate.ToShortDateString() + " " + _document.UpdateDate.ToShortTimeString();

            if (_cControl.DoesPublish == false)
                ClientTools.ShowSpeechBubble(speechBubbleIcon.save, ui.Text("speechBubbles", "editContentSavedHeader", null), ui.Text("speechBubbles", "editContentSavedText", null));

            ClientTools.SyncTree(_document.Path, true);
        }

        protected void SendToPublish(object sender, EventArgs e)
        {
            if (Page.IsValid)
            {
                ClientTools.ShowSpeechBubble(speechBubbleIcon.save, ui.Text("speechBubbles", "editContentSendToPublish", base.getUser()), ui.Text("speechBubbles", "editContentSendToPublishText", base.getUser()));
                _document.SendToPublication(getUser());
            }
        }

        protected void Publish(object sender, EventArgs e)
        {
            if (Page.IsValid)
            {
                if (_document.Level == 1 || new Document(_document.Parent.Id).PathPublished)
                {
                    var previouslyPublished = _document.Published;

                    Trace.Warn("before d.publish");

                    if (_document.PublishWithResult(base.getUser()))
                    {

                        ClientTools.ShowSpeechBubble(speechBubbleIcon.save, ui.Text("speechBubbles", "editContentPublishedHeader", null), ui.Text("speechBubbles", "editContentPublishedText", null));
                        library.UpdateDocumentCache(_document);

                        _littPublishStatus.Text = ui.Text("content", "lastPublished", base.getUser()) + ": " + _document.VersionDate.ToString() + "<br/>";

                        if (getUser().GetPermissions(_document.Path).IndexOf("U") > -1)
                            _unPublish.Visible = true;

                        
                        if (previouslyPublished == false)
                        {
                            _documentHasPublishedVersion = _document.HasPublishedVersion();

                            foreach (var descendant in _document.GetPublishedDescendants())
                            {
                                library.UpdateDocumentCache(descendant);
                            }
                        }

                    }
                    else
                    {
                        ClientTools.ShowSpeechBubble(speechBubbleIcon.warning, ui.Text("publish"), ui.Text("speechBubbles", "contentPublishedFailedByEvent"));
                    }
                }
                else
                    ClientTools.ShowSpeechBubble(speechBubbleIcon.warning, ui.Text("publish"), ui.Text("speechBubbles", "editContentPublishedFailedByParent"));

                // page cache disabled...
                //			cms.businesslogic.cache.Cache.ClearCacheObjectTypes("umbraco.page");


                // Update links
            }
        }

        protected void UnPublishDo(object sender, EventArgs e)
        {
            _document.UnPublish();
            _littPublishStatus.Text = ui.Text("content", "itemNotPublished", base.getUser());
            _unPublish.Visible = false;
            _documentHasPublishedVersion = false;

            library.UnPublishSingleNode(_document.Id);

            Current.ClientTools.SyncTree(_document.Path, true);
            ClientTools.ShowSpeechBubble(speechBubbleIcon.success, ui.Text("unpublish"), ui.Text("speechBubbles", "contentUnpublished"));

            //newPublishStatus.Text = "0";
        }

        void UpdateNiceUrlProperties(string niceUrlText, string altUrlsText)
        {
            _linkProps.Controls.Clear();

            var lit = new Literal();
            lit.Text = niceUrlText;
            _linkProps.addProperty(ui.Text("content", "urls", getUser()), lit);

            if (!string.IsNullOrWhiteSpace(altUrlsText))
            {
                lit = new Literal();
                lit.Text = altUrlsText;
                _linkProps.addProperty(ui.Text("content", "alternativeUrls", getUser()), lit);
            }
        }

        void UpdateNiceUrls()
        {
            if (!_documentHasPublishedVersion)
            {
                UpdateNiceUrlProperties("<i>" + ui.Text("content", "itemNotPublished", base.getUser()) + "</i>", null);
                return;
            }

            var niceUrlProvider = Umbraco.Web.UmbracoContext.Current.RoutingContext.NiceUrlProvider;
            var url = niceUrlProvider.GetNiceUrl(_document.Id);
            string niceUrlText = null;
            var altUrlsText = new System.Text.StringBuilder();

            if (url == "#")
            {
                // document as a published version yet it's url is "#" => a parent must be
                // unpublished, walk up the tree until we find it, and report.
                var parent = _document;
                do
                {
                    parent = parent.ParentId > 0 ? new Document(parent.ParentId) : null;
                }
                while (parent != null && parent.Published);

                if (parent == null) // oops - internal error
                    niceUrlText = "<i>" + ui.Text("content", "parentNotPublishedAnomaly", base.getUser()) + "</i>";
                else
                    niceUrlText = "<i>" + ui.Text("content", "parentNotPublished", parent.Text, base.getUser()) + "</i>";
            }
            else
            {
                niceUrlText = string.Format("<a href=\"{0}\" target=\"_blank\">{0}</a>", url);

                foreach (var altUrl in niceUrlProvider.GetAllAbsoluteNiceUrls(_document.Id).Where(u => u != url))
                    altUrlsText.AppendFormat("<a href=\"{0}\" target=\"_blank\">{0}</a><br />", altUrl);
            }

            UpdateNiceUrlProperties(niceUrlText, altUrlsText.ToString());
        }

        /// <summary>
        /// Clears the page of all controls and shows a simple message. Used if users don't have visible access to the page.
        /// </summary>
        /// <param name="message"></param>        
        private void ShowUserValidationError(string message)
        {
            this.Controls.Clear();
            this.Controls.Add(new LiteralControl(String.Format("<h1>{0}</h1>", message)));
        }

        /// <summary>
        /// Checks if the user cannot view/browse this page/app and displays an html message to the user if this is not the case.
        /// </summary>
        /// <returns></returns>
        private bool CheckUserValidation()
        {
            // Validate permissions
            if (!ValidateUserApp("content"))
            {
                ShowUserValidationError("The current user doesn't have access to this application. Please contact the system administrator.");
                return false;
            }
            if (!ValidateUserNodeTreePermissions(_document.Path, ActionBrowse.Instance.Letter.ToString()))
            {
                ShowUserValidationError("The current user doesn't have permissions to browse this document. Please contact the system administrator.");
                return false;
            }
            //TODO: Change this, when we add view capabilities, the user will be able to view but not edit!
            if (!ValidateUserNodeTreePermissions(_document.Path, ActionUpdate.Instance.Letter.ToString()))
            {
                ShowUserValidationError("The current user doesn't have permissions to edit this document. Please contact the system administrator.");
                return false;
            }
            return true;
        }

        private void AddPreviewButton(uicontrols.ScrollingMenu menu, int id)
        {
            menu.InsertSplitter(2);
            var menuItem = menu.NewIcon(3);
            menuItem.ImageURL = SystemDirectories.Umbraco + "/images/editor/vis.gif";
            // Fix for U4-682, if there's no template, disable the preview button
            if (_document.Template != -1)
            {
                menuItem.AltText = ui.Text("buttons", "showPage", this.getUser());
                menuItem.OnClickCommand = "window.open('dialogs/preview.aspx?id=" + id + "','umbPreview')";
            }
            else
            {
                string showPageDisabledText = ui.Text("buttons", "showPageDisabled", this.getUser());
                if (showPageDisabledText.StartsWith("["))
                    showPageDisabledText = ui.GetText("buttons", "showPageDisabled", null, "en"); ;

                menuItem.AltText = showPageDisabledText;
                ((Image) menuItem).Attributes.Add("style", "opacity: 0.5");
            }
        }

    }
}
