﻿// Copyright (c) 2017 GreenWerx.org.
//Licensed under CPAL 1.0,  See license.txt  or go to http://greenwerx.org/docs/license.txt  for full license details.
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using GreenWerx.Data.Logging;
using GreenWerx.Managers;
using GreenWerx.Managers.Events;
using GreenWerx.Managers.Geo;
using GreenWerx.Managers.Membership;

using GreenWerx.Models.App;
using GreenWerx.Models.Datasets;
using GreenWerx.Models.Events;
using GreenWerx.Models.Flags;
using GreenWerx.Models.Geo;
using GreenWerx.Models.Membership;
using GreenWerx.Utilites.Extensions;
using GreenWerx.Utilites.Helpers;
using GreenWerx.Utilites.Security;
using GreenWerx.Web.api.Helpers;
using GreenWerx.Web.Filters;
using GreenWerx.Web.Models;
using GreenWerx.WebAPI.Models;
using WebApi.OutputCache.V2;
using WebApiThrottle;

namespace GreenWerx.Web.api.v1
{
    [CacheOutput(ClientTimeSpan = 100, ServerTimeSpan = 100)]
    public class AppsController : ApiBaseController
    {
        private readonly SystemLogger _logger = null;

        public AppsController()
        {
            _logger = new SystemLogger(Globals.DBConnectionKey);
        }

        #region TODO refactor into own controller

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 0)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Apps/{appType}/Status")]
        public ServiceResult AppStatus(string appType)
        {
            if (string.IsNullOrWhiteSpace(appType))
                return ServiceResponse.Error("You must send an appType to check.");

            if (Globals.Application.Status == "RUNNING")
            {
                NetworkHelper _network = new NetworkHelper();
                string ip = _network.GetClientIpAddress(Request);
                _logger.InsertInfo("ip:" + ip + " appType:" + appType, "AppsController", "AppStatus");
            }
            string status = "NOT IMPLEMENTED";

            switch (appType.ToUpper())
            {
                case "WEB":
                    status = Globals.Application.Status;
                    break;
                    //todo. here we can call methods to check on other apps/services..
            }

            return ServiceResponse.OK("", status);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.HttpPost]
        [System.Web.Http.HttpDelete]
        [System.Web.Http.Route("api/Apps/Settings/Delete")]
        public ServiceResult Delete(Setting n)  //Setting setting)
        {
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));
            return am.Delete(n);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.HttpPost]
        [System.Web.Http.HttpDelete]
        [System.Web.Http.Route("api/Apps/Settings/Delete/{settingUUID}")]
        public ServiceResult DeleteSetting(string settingUUID)  //Setting setting)
        {
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));
            return am.DeleteSetting(settingUUID);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 5)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Apps/Settings/{name}")]
        public ServiceResult Get(string name) //todo permissions
        {
            User u = this.GetUser(Request.Headers.Authorization?.Parameter);

            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));
            Setting s = am.GetSetting(name);
            if (s == null)
                return ServiceResponse.Error("Settings name could not be found:" + name);

            RoleManager _roleManager = new RoleManager(Globals.DBConnectionKey, u);
            if (!_roleManager.DataAccessAuthorized(s, u, "get", false))
                return ServiceResponse.Error("You are not authorized to access this data.");

            return ServiceResponse.OK("", JsonConvert.SerializeObject(s));
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Apps/SettingsBy/{uuid}")]
        public ServiceResult GetBy(string uuid)
        {
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));
            Setting s = am.Get(uuid);
            if (s == null)
                return ServiceResponse.Error("Settings by could not find:" + uuid);
            return ServiceResponse.OK("", JsonConvert.SerializeObject(s));
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 0)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Apps/PublicSetting/{name}")]
        public ServiceResult GetPublicSetting(string name) //todo permissions
        {
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));
            Setting s = am.GetSetting(name);
            if (s == null)
                return ServiceResponse.Error("Publis setting doesn't exist:" + name);

            if (s.Private || s.RoleWeight > 0)
                return ServiceResponse.Error("You are not authorized to access this data.");

            return ServiceResponse.OK("", JsonConvert.SerializeObject(s));
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Public/Settings")]
        public ServiceResult GetPublicSettings()
        {
            if (Globals.Application.Status == "INSTALLING")
                return ServiceResponse.Error("Application is installing, general settings are not available yet.");

            //to make sure we're in sync with the locations table we'll use the default online store locations account uuid to get the payment options for the sites account.
            LocationManager lm = new LocationManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            Location location = lm.GetAll()?.FirstOrDefault(w => w.isDefault == true && w.LocationType.EqualsIgnoreCase("ONLINE STORE"));

            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));

            List<dynamic> settings = am.GetPublicSettings("web", location?.AccountUUID).Cast<dynamic>().ToList();

            DataFilter filter = this.GetFilter(Request);
            settings = settings.Filter(ref filter);

            return ServiceResponse.OK("", settings, filter.TotalRecordCount);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Settings")]
        public ServiceResult GetSettings()
        {
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));

            List<dynamic> settings = am.GetAppSettings("web").Cast<dynamic>().ToList();

            DataFilter filter = this.GetFilter(Request);
            settings = settings.Filter(ref filter);

            return ServiceResponse.OK("", settings, filter.TotalRecordCount);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Settings/Add")]
        [System.Web.Http.Route("api/Apps/Settings/Insert")]
        public ServiceResult Insert(Setting n)
        {
            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            if (n == null)
                return ServiceResponse.Error("No record sent.");

            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));

            var setting = (Setting)n;

            if (string.IsNullOrWhiteSpace(setting.AppType))
                setting.AppType = "web";

            if (string.IsNullOrWhiteSpace(setting.SettingClass))
                setting.SettingClass = "app";

            setting.DateCreated = DateTime.UtcNow;
            setting.CreatedBy = CurrentUser.UUID;
            setting.AccountUUID = CurrentUser.AccountUUID;
            string encryptionKey = Globals.Application.AppSetting("AppKey");
            if (am.Insert(setting, encryptionKey).Code == 200)
                return ServiceResponse.OK("", setting);

            return ServiceResponse.Error("Failed to save the setting.");
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpPost]
        [System.Web.Http.HttpPatch]
        [System.Web.Http.Route("api/Apps/Settings/Update")]
        public ServiceResult Update(Setting form)
        {
            if (form == null)
                return ServiceResponse.Error("Invalid Setting sent to server.");

            AppManager AppManager = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));

            var dbS = (Setting)AppManager.Get(form.UUID);

            if (dbS == null)
                return ServiceResponse.Error("Setting was not found.");

            if (dbS.DateCreated == DateTime.MinValue)
                dbS.DateCreated = DateTime.UtcNow;

            dbS.Name = form.Name;
            dbS.Value = form.Value;
            dbS.Deleted = form.Deleted;
            dbS.Status = form.Status;
            dbS.SortOrder = form.SortOrder;

            string encryptionKey = Globals.Application.AppSetting("AppKey");
            return AppManager.Update(dbS, encryptionKey);
        }

        #endregion TODO refactor into own controller

        //Install Step 2
        //Save Account info
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Install/Accounts")]
        public ServiceResult CreateAccounts(AppInfo appSettings)
        {
            if (IsInstallReady() == false)
                return ServiceResponse.Error("Website is not ready to be installed. Check the intall.json file.");

            if (string.IsNullOrWhiteSpace(appSettings.UserName))
                return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Username is empty." };

            if (appSettings.UserPassword != appSettings.ConfirmPassword)
                return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Passwords don't match." };

            if (PasswordHash.CheckStrength(appSettings.UserPassword) < PasswordHash.PasswordScore.Medium)
                return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Password is too weak." };

            if (PasswordHash.CheckStrength(appSettings.UserPassword) < PasswordHash.PasswordScore.Strong)
                return ServiceResponse.Error("Your password is weak. Try again.");

            WebApplication wa = new WebApplication();
            if (wa.SaveConfigSetting("SiteAdmins", appSettings.UserName?.ToLower()).Code != 200)
                return ServiceResponse.Error("Error saving SiteAdmins to .config:" + appSettings.UserName);

            //Create the initial account as the domain
            if (string.IsNullOrWhiteSpace(appSettings.AccountName))
                appSettings.AccountName = appSettings.SiteDomain;

            if (string.IsNullOrWhiteSpace(appSettings.UserEmail))
                return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Email is empty." };

            if (string.IsNullOrWhiteSpace(appSettings.AccountEmail))
                appSettings.AccountEmail = appSettings.UserEmail;

            if (string.IsNullOrWhiteSpace(appSettings.SecurityQuestion))
                return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Security question is empty." };

            if (string.IsNullOrWhiteSpace(appSettings.UserSecurityAnswer))
                return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Security answer is empty." };

            if (string.IsNullOrWhiteSpace(Globals.DBConnectionKey)) //appSettings.ActiveDbConnectionKey))
                return ServiceResponse.Error("ActiveDbConnectionKey is not set. This must be set to save values to the database.");

            AppManager am = new AppManager(Globals.DBConnectionKey, "web", "");
            return am.CreateAccounts(appSettings);
        }

        //Install Step 1
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Install/CreateDatabase")]
        public ServiceResult CreateDatabase(AppInfo appSettings)
        {
            if (IsInstallReady() == false)
                return ServiceResponse.Error("Website is not ready to be installed. Check the intall.json file.");

            AppManager am = new AppManager(Globals.DBConnectionKey, "web", "");
            string connectionString = am.CreateConnectionString(appSettings);

            if (string.IsNullOrWhiteSpace(connectionString))
                return ServiceResponse.Error("Failed to create a database connectionstring.");

            if (appSettings.ActiveDbProvider != "SQLITE")
            {
                if (string.IsNullOrWhiteSpace(appSettings.ActiveDbUser))
                    return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Database Username is empty." };

                if (string.IsNullOrWhiteSpace(appSettings.ActiveDbPassword))
                    return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Database password is empty." };

                if (string.IsNullOrWhiteSpace(appSettings.ActiveDatabase))
                    return new ServiceResult() { Code = 500, Status = "ERROR", Message = "Database name is empty." };
            }

            string providerName = am.GetDbProviderName(appSettings.ActiveDbProvider);

            if (string.IsNullOrWhiteSpace(providerName))
                return ServiceResponse.Error("Failed to create a database providerName.");

            if (!Globals.Application.SaveConnectionString(appSettings.ActiveDbProvider, connectionString, providerName))
                return ServiceResponse.Error("Failed to save the connection string to the .config file.");

            appSettings.ActiveDbConnectionKey = appSettings.ActiveDbProvider; //set this so after the install it has something to reference.

            Globals.DBConnectionKey = appSettings.ActiveDbConnectionKey;

            //Sets the connection key
            if (Globals.Application.SaveConfigSetting("DefaultDbConnection", appSettings.ActiveDbProvider).Code != 200)
                return ServiceResponse.Error("Failed to save .config setting DefaultDbConnection for provider:" + appSettings.ActiveDbProvider);

            ////if (!string.IsNullOrWhiteSpace(appSettings.ActiveDatabase))
            ////    appSettings.ActiveDbPassword = Cipher.Crypt(appSettings.AppKey, appSettings.ActiveDbPassword, true);

            return am.CreateDatabase(appSettings, connectionString);
        }

        //Install Step: finalize
        //Add, set and cleanup last of the installation settings
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Install/Finalize")]
        public ServiceResult FinalizeInstall(AppInfo appSettings)
        {
            if (string.IsNullOrWhiteSpace(Globals.DBConnectionKey))// appSettings.ActiveDbConnectionKey))
                return ServiceResponse.Error("Unable to save to database. Connection key is not set.");

            UserManager um = new UserManager(Globals.DBConnectionKey, "");
            User user = um.GetUserByEmail(appSettings.UserEmail);

            string appKey = Globals.Application.AppSetting("AppKey"); //this should pull from config file. UseDatabaseConfig should be false.
                                                                      //After Config file is saved try saving to  database.

            WebApplication wa = new WebApplication();
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", "");
            am.Installing = true;

            string protocol = HttpContext.Current.Request.IsSecureConnection == true ? "https://" : "http://";
            string siteDomain = protocol + appSettings.SiteDomain;

            am.Insert(new Setting()
            {
                AccountUUID = user.AccountUUID,
                CreatedBy = user.UUID,
                Active = true,
                Deleted = false,
                AppType = "web",
                Value = siteDomain,
                Name = "AllowedOrigin",
                SettingClass = "string",
                RoleWeight = RoleFlags.MemberRoleWeights.Owner, //only owner can modify
                RoleOperation = ">=",
                DateCreated = DateTime.UtcNow,
                Type = "string"
            }, "");

            wa.SaveConfigSetting("AllowedOrigin", siteDomain);
      
            //WebApplication THIS HAS SAVE TO CONFIG
            Globals.Application.ApiStatus = "PRIVATE";
            


          

            IEnumerable<string> originValues;

            //  this.RequestContext.Url.
            //this is the installers uri. It's added so we can test it (login etc) after installed. 
            //Incase it's differen from the host.
            string origins = HttpContext.Current.Request.UrlReferrer.Scheme + "://" + HttpContext.Current.Request.UrlReferrer.Authority + ",";// + HttpContext.Current.Request.UrlReferrer.AbsolutePath.TrimEnd('/') + "/";
           // return new MvcHtmlString(str.ToHtmlString().Replace("value=\"\"", String.Format("value=\"{0}\"", referrer)));
            //string host = ""; // protocol + appSettings.SiteDomain;
            //Request.Headers.TryGetValues("Origin", out originValues);
            //foreach (string host in originValues)
            //{
            //    if ( string.IsNullOrWhiteSpace(host) == false && !siteDomain.Contains(host))
            //    {
            //        Globals.Application.ApiStatus = "PROTECTED";
            //        origins += protocol + host + ",";
            //    }
            //}

            Request.Headers.TryGetValues("Host", out originValues);
            foreach (string origin in originValues)
            {
                //the installer is not on the same domain, so we need to be able to access it from the domain.
                //allow cors from the orginating domain
                if (!siteDomain.Contains(origin))
                {
                    Globals.Application.ApiStatus = "PROTECTED";
                    origins += origins + ",";
                }

               // if (am.SettingExistsInDatabase("AllowedOrigin", origin))
                    //continue;
                
                //am.Insert(new Setting()
                //{
                //    AccountUUID = user.AccountUUID,
                //    CreatedBy = user.UUID,
                //    Active = true,
                //    Deleted = false,
                //    AppType = "web",
                //    Value = origin,
                //    Name = "AllowedOrigin",
                //    SettingClass = "string",
                //    RoleWeight = RoleFlags.MemberRoleWeights.Owner,
                //    RoleOperation = ">=",
                //    DateCreated = DateTime.UtcNow,
                //    Type = "string"
                //}, "");
             //   wa.SaveConfigSetting("AllowedOrigins", origin);
            }
            origins = origins.Remove(origins.Length - 1, 1);// remove trailing comma.
            wa.SaveConfigSetting("AllowedOrigins", origins);
            ServiceResult res;

            if (wa.SaveConfigSetting("ApiStatus", Globals.Application.ApiStatus).Code != 200)
                return ServiceResponse.Error("Failed to save ApiStatus:" + Globals.Application.ApiStatus);

            //Add to the database because the import won't catch it in time for the file to update.
            am.Insert(new Setting()
            {
                AccountUUID = user.AccountUUID,
                CreatedBy = user.UUID,
                Active = true,
                Deleted = false,
                AppType = "web",
                Value = Globals.Application.ApiStatus,
                Name = "ApiStatus",
                SettingClass = "string",
                RoleWeight = RoleFlags.MemberRoleWeights.Owner,
                RoleOperation = ">=",
                DateCreated = DateTime.UtcNow,
                Type = "string"
            }, "");
            wa.SaveConfigSetting("ApiStatus", Globals.Application.ApiStatus);
            am.SeedDataSetAccount(user.AccountUUID);
            //TODO ONE OF THE SETTINGS WAS EMPTY
            //copy all the app settings to the database settings.
            //res = wa.ImportWebConfigToDatabase(user, appKey, true, true);
            //if (res.Code != 200)
            //    return res;

            //read back the settings and validate to make sure it's all good.
            AppInfo app = am.GetAppInfo(user.AccountUUID, "web");
            if (app == null)
                return ServiceResponse.Error("App settings could not be found.");


            //Confirm the minimum settings required to run this thing..
            res = am.ValidateInstallSettings(app);
            if (res.Code != 200)
                return res;

            ////We need to make sure the file is gone before moving on because
            ////the status flag may not get reset correctly to running.
            string directory = EnvironmentEx.AppDataFolder;
            string pathToCommands = Path.Combine(directory, "Install\\install.json");
            try
            {
                File.Delete(pathToCommands);
            }
            catch (Exception ex)
            {
                return ServiceResponse.Error(ex.Message);
            }
            bool fileDeleted = false;
            int waitCount = 1;
            do
            {
                if (!File.Exists(pathToCommands))
                    fileDeleted = true;

                if (!fileDeleted)
                    Thread.Sleep(1000);

                waitCount++;

                if (waitCount > 6)
                    return ServiceResponse.Error("Unable to delete install.json file.");
            }
            while (!fileDeleted);

            Globals.Application.Status = "RUNNING";
            //Globals.Application.UseDatabaseConfig = true;

            return ServiceResponse.OK();
        }

        [System.Web.Http.HttpPost]
        [System.Web.Http.AllowAnonymous]
        // [EnableThrottling(PerSecond = 3)]
        [System.Web.Http.Route("api/Apps/Dashboard/{view}")]
        public ServiceResult GetDashboard(string view)
        {
            if (string.IsNullOrEmpty(view))
                return ServiceResponse.Error("View is empty.");

            if (Globals.Application.Status == "INSTALLING")
                return ServiceResponse.Error("Application is installing, dashboard is not available yet.");

            view = view.ToLower();
            DataFilter filter = this.GetFilter(Request);
            string options = "";
            try
            {
                Task<string> content = Request.Content.ReadAsStringAsync();
                if (content != null)
                {
                    options = content.Result;
                }
            }
            catch (Exception ex)
            {
                Debug.Assert(false, ex.Message);
                return ServiceResponse.Error(ex.Message);
            }

            Dashboard db = new Dashboard();
            db.View = view;
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", Request?.Headers?.Authorization?.Parameter);
            db.Domain = am.GetSetting("SiteDomain")?.Value;

            db.Content = am.GetAppSettings("web")?.Where(w => w.AccountUUID == CurrentUser.AccountUUID && w.Deleted == false && w.Private == false && w.RoleWeight == 0)
                 .Select(s => new KeyValuePair<string, string>(s.Name, s.Value)).ToList();

            switch (view)
            {
                case "termsofservice":
                    string tos = this.GetTemplate("TermsOfService", options).Result?.ToString();
                    db.Content.Add(new KeyValuePair<string, string>("TermsOfService", tos));
                    break;

                case "privacy":
                    break;

                case "events":
                    return ServiceResponse.OK("", BuildEventsDashboard(options, ref filter));

                default:
                    db.Title = db.Domain;
                    BuildMenu(view, ref db);
                    break;
            }
            return ServiceResponse.OK("Dashboard." + view, db);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Apps/DataTypes")]
        public ServiceResult GetDataTypes()
        {
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));
            return am.DataTypes();
        }

        //[ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        //[System.Web.Http.HttpGet]
        //[System.Web.Http.Route("api/Apps/SettingSource")]
        //public ServiceResult GetSettingSource()
        //{
        //    string res = "web.config";
        //    if (Globals.Application.UseDatabaseConfig)
        //        res = "Database";

        //    return ServiceResponse.OK(res);
        //}

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Apps/TableNames")]
        public ServiceResult GetTableNames()
        {
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));
            return am.TableNames();
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.AllowAnonymous]
        // [EnableThrottling(PerSecond = 3)]
        [System.Web.Http.Route("api/Apps/Template/{templateName}/Replace/{replaceOptions}")]
        public ServiceResult GetTemplate(string templateName, string replaceOptions)
        {
            string root = EnvironmentEx.AppDataFolder;

            string pathToFile = Path.Combine(root, "Templates", templateName + ".html");

            if (!File.Exists(pathToFile))
                return ServiceResponse.Error("Template not found.");

            string template = "";

            try
            {
                template = File.ReadAllText(pathToFile);

                if (string.IsNullOrWhiteSpace(template))
                    return ServiceResponse.OK("Template is empty.");

                List<ApiCommand> cmds = JsonConvert.DeserializeObject<List<ApiCommand>>(replaceOptions);

                foreach (ApiCommand cmd in cmds)
                {
                    switch (cmd.Command?.ToLower())
                    {
                        case "replace":
                            foreach (KeyValuePair<string, string> keyValue in cmd.Arguments)
                            {
                                template = template.Replace(keyValue.Key, keyValue.Value);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                return ServiceResponse.Error(ex.Message);
            }

            return ServiceResponse.OK(templateName, template);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Import/Item/")]
        public ServiceResult ImportDataItem(Setting n)
        {
            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            //use the current uuid to get the default data.
            //create a new uuid and insert
            //if account uuid default account set the created by to default account
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", Request?.Headers?.Authorization?.Parameter);

            return am.ImportItem(n);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpDelete]
        [System.Web.Http.Route("api/Apps/Tables/{tableName}/DeleteItem/{itemUUID}")]
        public ServiceResult ImportDataItem(string tableName, string itemUUID)
        {
            //use the current uuid to get the default data.
            //create a new uuid and insert
            //if account uuid default account set the created by to default account
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", Request?.Headers?.Authorization?.Parameter);

            return am.Delete(tableName, itemUUID);
        }

        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Install")]
        public ServiceResult Install(AppInfo appSettings)
        {
            if (IsInstallReady() == false)
                return ServiceResponse.Error("Website is not ready to be installed. Check the intall.json file.");

            Globals.Application.Status = "INSTALLING";
            //Globals.Application.UseDatabaseConfig = false;

            if (string.IsNullOrWhiteSpace(appSettings.AppKey))
                appSettings.AppKey = PasswordHash.CreateHash(Guid.NewGuid().ToString("N"));

            ServiceResult res = Globals.Application.InitializeConfigFile(appSettings);

            if (res.Code != 200)
                return res;

            AppManager am = new AppManager(Globals.DBConnectionKey, "web", "");
            res = am.Install(appSettings);

            if (res.Code != 200)
                return res;

            User user = (User)res.Result;

            res = Globals.Application.ImportWebConfigToDatabase(user, appSettings.AppKey, true, true);
            if (res.Code != 200)
                return res;

            string protocol = HttpContext.Current.Request.IsSecureConnection == true ? "https://" : "http://";
            string siteDomain = protocol + appSettings.SiteDomain;

            am.Insert(new Setting()
            {
                AccountUUID = user.AccountUUID,
                CreatedBy = user.UUID,
                Active = true,
                Deleted = false,
                AppType = "web",
                Value = siteDomain,
                Name = "AllowedOrigin",
                SettingClass = "string",
                RoleWeight = RoleFlags.MemberRoleWeights.Owner,
                RoleOperation = ">=",
                Type = "string"
            }, "");

            IEnumerable<string> originValues;
            Request.Headers.TryGetValues("Origin", out originValues);
            foreach (string origin in originValues)
            {
                if (am.SettingExistsInDatabase("AllowedOrigin", origin))
                    continue;

                am.Insert(new Setting()
                {
                    AccountUUID = user.AccountUUID,
                    CreatedBy = user.UUID,
                    Active = true,
                    Deleted = false,
                    AppType = "web",
                    Value = origin,
                    Name = "AllowedOrigin",
                    SettingClass = "string",
                    RoleWeight = 4,
                    RoleOperation = ">=",
                    Type = "string"
                }, "");
            }

            Globals.Application.ApiStatus = "PRIVATE";
#if DEBUG
            Globals.Application.ApiStatus = "PROTECTED";//using NG server will cut off access because it's on a different port.
#endif

            if (am.GetSetting("ApiStatus") == null)
            {
                am.Insert(new Setting()
                {
                    AccountUUID = user.AccountUUID,
                    CreatedBy = user.UUID,
                    Active = true,
                    Deleted = false,
                    AppType = "web",
                    Value = Globals.Application.ApiStatus,
                    Name = "ApiStatus",
                    SettingClass = "string",
                    RoleWeight = RoleFlags.MemberRoleWeights.Owner,
                    RoleOperation = ">=",
                    Type = "string"
                }, "");
            }

            Globals.Application.Status = "RUNNING";
            //Globals.Application.UseDatabaseConfig = true;
            return res;
        }

        //Install Step 1.A
        // call this from client right after the databaase is created
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Install/SaveSettings")]
        public ServiceResult SaveSettings(AppInfo appSettings)
        {
            if (IsInstallReady() == false)
                return ServiceResponse.Error("Website is not ready to be installed. Check the intall.json file.");

            if (string.IsNullOrWhiteSpace(appSettings.AppKey))
                appSettings.AppKey = PasswordHash.CreateHash(Guid.NewGuid().ToString("N"));

            WebApplication wa = new WebApplication();

            if (string.IsNullOrWhiteSpace(appSettings.AppKey))
                appSettings.AppKey = PasswordHash.CreateHash(Guid.NewGuid().ToString("N"));

            if (wa.SaveConfigSetting("AppKey", appSettings.AppKey.Replace(":", "")).Code != 200)
                return ServiceResponse.Error("Failed to save AppKey:" + appSettings.AppKey);

            if (string.IsNullOrWhiteSpace(appSettings.AppType))
                appSettings.AppType = "web";

            //This will create permissions for request paths as the come in. Should only be used when adding a new
            //controller/feature.
            //
            if (wa.SaveConfigSetting("AddRequestPermissions", "false").Code != 200)
                return ServiceResponse.Error("Failed to save AddRequestPermissions.");

            if (wa.SaveConfigSetting("SiteDomain", appSettings.SiteDomain).Code != 200)
                return ServiceResponse.Error("Failed to save SiteDomain:" + appSettings.SiteDomain);

            if (wa.SaveConfigSetting("ApiVersion", "1.0").Code != 200)//backlog: have it look in the api folder to get the version
                return ServiceResponse.Error("Failed to save ApiVersion:1.0");

            if (wa.SaveConfigSetting("ClientValidationEnabled", "true").Code != 200)
                return ServiceResponse.Error("Failed to save ClientValidationEnabled:true");

            //if (wa.SaveConfigSetting("UseDatabaseConfig", "false"))
            //    return ServiceResponse.Error("Failed to save UseDatabaseConfig:false");

            string key = PasswordHash.CreateHash(Guid.NewGuid().ToString("N"));
            if (wa.SaveConfigSetting("DBBackupKey", key.Replace(":", "").Substring(0, 11)).Code != 200)
                return ServiceResponse.Error("Failed to save DBBackupKey:" + key.Replace(":", "").Substring(0, 11));

            if (wa.SaveConfigSetting("SessionLength", "60").Code != 200)
                return ServiceResponse.Error("Failed to save SessionLength:60");

            if (wa.SaveConfigSetting("TemplateEmailNewMember", "App_Data\\Templates\\Site\\EmailNewMember.html").Code != 200)
                return ServiceResponse.Error("Failed to save TemplateEmailNewMember:App_Data\\Templates\\Site\\EmailNewMember.html");

            if (wa.SaveConfigSetting("TemplatePasswordResetEmail", "App_Data\\Templates\\Site\\PasswordResetEmail.html").Code != 200)
                return ServiceResponse.Error("Failed to save TemplatePasswordResetEmail:App_Data\\Templates\\Site\\PasswordResetEmail.html");

            if (wa.SaveConfigSetting("TemplateUserInfoEmail", "App_Data\\Templates\\Site\\UserInfoEmail.html").Code != 200)
                return ServiceResponse.Error("Failed to save TemplateUserInfoEmail:App_Data\\Templates\\Site\\UserInfoEmail.html");

            if (wa.SaveConfigSetting("EmailStoreTemplateOrderStatusReceived", "App_Data\\Templates\\Store\\EmailOrderReceived.html").Code != 200)
                return ServiceResponse.Error("Failed to save EmailStoreTemplateOrderStatusReceived:App_Data\\Templates\\Store\\EmailOrderReceived.html");

            return ServiceResponse.OK("", appSettings);
        }

        //Install Step 1.B
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Apps/Install/SeedDatabase")]
        public ServiceResult SeedDatabase(AppInfo appSettings)
        {
            if (IsInstallReady() == false)
                return ServiceResponse.Error("Website is not ready to be installed. Check the intall.json file.");

            string directory = EnvironmentEx.AppDataFolder;
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", "");
            am.Installing = true;
            return am.SeedDatabase(Path.Combine(directory, "Install\\SeedData\\"));
        }

        protected EventsDashboard BuildEventsDashboard(string options, ref DataFilter filter)
        {
            EventsDashboard dash = new EventsDashboard();
            string defaultEventUUID = "";//blank parent id will return main events. Globals.Application.AppSetting("DefaultEvent");
            EventManager eventManager = new EventManager(Globals.DBConnectionKey, this.GetAuthToken(Request));

            //get events starting from midnight today
            var events = eventManager.GetSubEvents(defaultEventUUID, DateTime.UtcNow, ref filter).Cast<dynamic>().ToList();
            dash.Events = events.Filter(ref filter);
            dash.Groups = eventManager.GetEventGroups(defaultEventUUID);
            dash.Inventory = eventManager.GetEventInventory(defaultEventUUID);
            dash.Members = eventManager.GetEventMembers(defaultEventUUID, CurrentUser.AccountUUID);
            dash.Locations = eventManager.GetEventLocations(defaultEventUUID);

            return dash;
        }

        protected void BuildMenu(string view, ref Dashboard db)
        {
            //todo move data to settings table. make SettingsClass = "MenuItem" or "List<MenuItem>"
            //   this way we can set roleweight and operation. Store it in value as json string
            //   make the key = view name, value is entire menu for that key in json format?
            switch (view)
            {
                case "navbar_default":
                    db.TopMenuItems.Add(new MenuItem()
                    {
                        href = "/",
                        label = "Home",
                        type = "link",
                        icon = "fa fa-house"
                    });
                    db.TopMenuItems.Add(new MenuItem()
                    {
                        href = "/store/",
                        label = "Store",
                        type = "link",
                        icon = "fa fa-shopping-bag"
                    });
                    db.TopMenuItems.Add(new MenuItem()
                    {
                        href = "/about",
                        label = "About",
                        type = "link",
                        icon = "fa fa-question"
                    });
                    break;

                #region navbar_admin

                case "navbar_admin":
                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/",
                        label = "Dashboard",
                        type = "link",
                        icon = "fa fa-dashboard"
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/system", //items from Models/App folder go here
                        label = "System",
                        type = "link",
                        icon = "fa fa-cogs",
                        items = new List<MenuItem>()
                        {
                            new MenuItem() {   href = "/system/settings", label = "Settings", type = "link" }
                        }
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/utilities",//functions from api/ToolsController go here
                        label = "Utilities",
                        type = "link",
                        icon = "fa fa-cogs",
                        items = new List<MenuItem>()
                        {
                            new MenuItem() {   href = "/utilities/tools", label = "Tools", type = "link" }
                        }
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/finance",
                        label = "Finance",
                        type = "link",
                        icon = "fa fa-cogs",
                        items = new List<MenuItem>()
                        {
                            new MenuItem() {   href = "/finance/accounts",      label = "Accounts",     type = "link" },
                            new MenuItem() {   href = "/finance/pricerules",       label = "Price Rules",      type = "link" },
                            new MenuItem() {   href = "/finance/currency",      label = "Currency",     type = "link" },
                            //new MenuItem() {   href = "finance/fees",          label = "Fees",         type = "link" },
                            new MenuItem() {   href = "/finance/transactions",  label = "Transactions", type = "link" }
                        }
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/store",
                        label = "Store",
                        type = "link",
                        icon = "fa fa-cogs",
                        items = new List<MenuItem>()
                        {
                            ////new MenuItem() {   href = "/coupons", label = "Coupons", type = "link" },
                            new MenuItem() {   href = "/store/departments", label = "Departments", type = "link" },
                            new MenuItem() {   href = "/store/orders", label = "Orders", type = "link" },
                            ////new MenuItem() {   href = "/shipping", label = "Shipping", type = "link" },
                              ////,new MenuItem() {   href = "/vendors", label = "vendors", type = "link" }
                            new MenuItem() {   href = "/store/payoptions", label = "Payment Options", type = "link" }
                        }
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/identities",
                        label = "Identity Management",
                        type = "link",
                        icon = "fa fa-cogs",
                        items = new List<MenuItem>()
                        {
                            new MenuItem() {   href = "/membership/users", label = "Users", type = "link" },
                            new MenuItem() {   href = "/membership/accounts", label = "Accounts", type = "link" },
                            new MenuItem() {   href = "/membership/roles", label = "Roles", type = "link" }
                        }
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/events/events", //todo change to /events/dashboard  when added to client
                        label = "Events",
                        type = "link",
                        icon = "fa fa-cogs",
                        items = new List<MenuItem>()
                        {
                            new MenuItem() {   href = "/events/locations", label = "Locations", type = "link" }
                        }
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/general",
                        label = "Misc.",
                        type = "link",
                        icon = "fa fa-cogs",
                        items = new List<MenuItem>()
                        {
                            new MenuItem() {   href = "/general/measures", label = "Measures", type = "link" },
                            new MenuItem() {   href = "/general/categories", label = "Categories", type = "link" },
                            new MenuItem() {   href = "/general/attributes", label = "Attributes", type = "link" }
                        }
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/assets",
                        label = "Assets",
                        type = "link",
                        icon = "fa fa-cogs",
                        items = new List<MenuItem>()
                        {
                            new MenuItem() {   href = "/assets/inventory", label = "Inventory", type = "link" },
                            new MenuItem() {   href = "/assets/locations", label = "Locations", type = "link" },
                            new MenuItem() {   href = "/assets/products", label = "Products", type = "link" },
                            new MenuItem() {   href = "/assets/strains", label = "Strains", type = "link" }
                        }
                    });

                    db.SideMenuItems.Add(new MenuItem()
                    {
                        href = "/about",
                        label = "About",
                        type = "link",
                        icon = "fa fa-question"
                    });
                    break;

                #endregion navbar_admin

                default:
                    db.TopMenuItems.Add(new MenuItem()
                    {
                        href = "/",
                        label = "Home",
                        type = "link",
                        icon = "fa fa-home"
                    });
                    db.TopMenuItems.Add(new MenuItem()
                    {
                        href = "/store",
                        label = "Store",
                        type = "link",
                        icon = "fa fa-shopping-bag"
                    });
                    db.TopMenuItems.Add(new MenuItem()
                    {
                        href = "/about",
                        label = "About",
                        type = "link",
                        icon = "fa fa-question"
                    });
                    break;
            }

            db.SideMenuItems = db.SideMenuItems.OrderBy(o => o.label).ToList();
            ////db.TopMenuItems = db.TopMenuItems.OrderBy(o => o.label).ToList();
        }

        private bool IsInstallReady()
        {
            string pathToInstallCommands = EnvironmentEx.AppDataFolder + "\\Install\\install.json";

            if (!File.Exists(pathToInstallCommands))
                return false;

            return true;
        }
    }
}