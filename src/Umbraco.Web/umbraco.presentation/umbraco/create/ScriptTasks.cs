using Umbraco.Core.IO;
using Umbraco.Web.UI;
using umbraco.BusinessLogic;
using umbraco.BasePages;

namespace umbraco
{
    public class ScriptTasks : LegacyDialogTask
    {
     
        public override bool PerformSave()
        {
            var scriptFileAr = Alias.Split('¤');
            
            var relPath = scriptFileAr[0];
            var fileName = scriptFileAr[1];
            var fileType = scriptFileAr[2];

            var createFolder = ParentID;

            var basePath = IOHelper.MapPath(SystemDirectories.Scripts + "/" + relPath + fileName);
            if (System.IO.File.Exists(basePath))
            {
                _returnUrl = string.Format("settings/scripts/editScript.aspx?file={0}{1}.{2}", relPath, fileName, fileType);
                return true;
            }

            if (createFolder == 1)
            {
                System.IO.Directory.CreateDirectory(basePath);
            }
            else
            {
                System.IO.File.Create(basePath + "." + fileType).Close();
                _returnUrl = string.Format("settings/scripts/editScript.aspx?file={0}{1}.{2}", relPath, fileName,
                                           fileType);
            }
            return true;
        }

        public override bool PerformDelete()
        {
            var path = IOHelper.MapPath(SystemDirectories.Scripts + "/" + Alias.TrimStart('/'));

            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
            else if (System.IO.Directory.Exists(path))
                System.IO.Directory.Delete(path, true);

            Log.Add(LogTypes.Delete, UmbracoEnsuredPage.CurrentUser, -1, Alias + " Deleted");
            return true;
        }

        private string _returnUrl = "";

        public override string ReturnUrl
        {
            get { return _returnUrl; }
        }

        public override string AssignedApp
        {
            get { return DefaultApps.settings.ToString(); }
        }
    }
}
