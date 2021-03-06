﻿// Copyright (c) 2017 GreenWerx.org.
//Licensed under CPAL 1.0,  See license.txt  or go to http://greenwerx.org/docs/license.txt  for full license details.
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GreenWerx.Data.Logging;
using GreenWerx.Data.Logging.Models;
using GreenWerx.Managers;
using GreenWerx.Managers.Documents;
using GreenWerx.Managers.Inventory;
using GreenWerx.Models.App;
using GreenWerx.Models.Datasets;
using GreenWerx.Models.Inventory;
using GreenWerx.Utilites.Extensions;
using GreenWerx.Web;
using GreenWerx.Web.api;
using GreenWerx.Web.Filters;

namespace GreenWerx.WebAPI.api.v1
{
    // [CacheOutput(ClientTimeSpan = 100, ServerTimeSpan = 100)]
    public class InventoryController : ApiBaseController
    {
        private readonly SystemLogger _logger = null;

        public InventoryController()
        {
            _logger = new SystemLogger(Globals.DBConnectionKey);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpDelete]
        [System.Web.Http.Route("api/Inventory/Delete")]
        public ServiceResult Delete(InventoryItem n)
        {
            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            if (n == null || string.IsNullOrWhiteSpace(n.UUID))
                return ServiceResponse.Error("Invalid account was sent.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            return inventoryManager.Delete(n);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpDelete]
        [System.Web.Http.Route("api/Inventory/Delete/{inventoryItemUUID}")]
        public ServiceResult Delete(string inventoryItemUUID)//todo bookmark latest test this.
        {
            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            var res = inventoryManager.Get(inventoryItemUUID);
            if (res.Code != 200)
                return res;

            InventoryItem p = (InventoryItem)res.Result;

            //TODO DELETE THE FILE FROM THE IMAGE FIELD, AND SETTINGS
            //Fire and forget delete task
            //Thread t = new Thread(() =>{
            string root = System.Web.HttpContext.Current.Server.MapPath("~/Content/Uploads/" + this.CurrentUser.UUID);

            DocumentManager dm = new DocumentManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            dm.DeleteImages(p, root);
            //});
            //t.Start();
            return inventoryManager.Delete(p);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpDelete]
        [System.Web.Http.Route("api/Inventory/Delete/{inventoryItemUUID}/File/{fileName}")]
        public ServiceResult DeleteInventoryFile(string inventoryItemUUID, string fileName)//todo bookmark latest test this.
        {
            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            var res = inventoryManager.Get(inventoryItemUUID);
            if (res.Code != 200)
                return res;

            InventoryItem p = (InventoryItem)res.Result;

            string root = System.Web.HttpContext.Current.Server.MapPath("~/Content/Uploads/" + this.CurrentUser.UUID);

            string pathToFile = Path.Combine(root, fileName);
            DocumentManager dm = new DocumentManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            if (dm.DeleteFile(p, pathToFile).Code != 200)
                return ServiceResponse.Error("Failed to delete file " + fileName);

            //now update the image field or delete the setting..
            if (p.Image.EqualsIgnoreCase(pathToFile))
            {
                p.Image = string.Empty; //todo v2? check settings for more images, automatically make the next image the primary(?) may not want to do this
                return this.Update(p);
            }

            //not the object field so it must be a setting.
            AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));

            List<Setting> settings = am.GetSettings(p.UUIDType)
                .Where(w => w.UUIDType.EqualsIgnoreCase("ImagePath") &&
                       w.Value == p.UUID &&
                       w.Image.Contains(fileName)).ToList();

            foreach (Setting setting in settings)
            {
                if (am.DeleteSetting(setting.UUID).Code != 200)
                    return ServiceResponse.Error("Failed to delete image setting for file " + fileName);
            }

            return ServiceResponse.OK();
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/InventoryBy/{uuid}")]
        public ServiceResult GetBy(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
                return ServiceResponse.Error("You must provide an id for the item.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            return inventoryManager.Get(uuid);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Item/{uuid}/Details")]
        public ServiceResult GetItemDetails(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
                return ServiceResponse.Error("You must provide an id for the item.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            return inventoryManager.GetItemDetails(uuid);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpPost]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Inventory/InventoryType/{type}")]
        public ServiceResult GetLocationsByInventoryType(string type)
        {
            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            List<dynamic> locations = (List<dynamic>)inventoryManager.GetItems(CurrentUser.AccountUUID)
                                                                    .Where(pw => (pw.AccountUUID == SystemFlag.Default.Account)
                                                                                    && (pw.ReferenceType?.EqualsIgnoreCase(type) ?? false)
                                                                                    && pw.Deleted == false
                                                                                    && string.IsNullOrWhiteSpace(pw.ReferenceType) == false)
                                                                                    .Cast<dynamic>().ToList();

            if (locations == null || locations.Count == 0)
                return ServiceResponse.Error("No locations available.");

            return ServiceResponse.OK("", locations);
        }

        [System.Web.Http.HttpPost]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Inventory/User/{uuid}")]
        public ServiceResult GetUserInventory(string uuid)
        {
            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            if (CurrentUser.UUID != uuid) //CurrentUser.SiteAdmin != true
                return ServiceResponse.Error("You are not authorized.");

            DataFilter filter = this.GetFilter(Request);
            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            List<dynamic> Inventory = (List<dynamic>)inventoryManager.GetUserItems(CurrentUser.AccountUUID, CurrentUser.UUID).Cast<dynamic>().ToList(); ;

            return ServiceResponse.OK("", Inventory);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Inventory/Add")]
        [System.Web.Http.Route("api/Inventory/Insert")]
        public ServiceResult Insert(InventoryItem n)
        {
            if (n == null)
                return ServiceResponse.Error("Invalid location posted to server.");

            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            if (string.IsNullOrWhiteSpace(n.CreatedBy))
            {
                n.CreatedBy = CurrentUser.UUID;
                n.AccountUUID = CurrentUser.AccountUUID;
                n.DateCreated = DateTime.UtcNow;
            }

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            return inventoryManager.Insert(n);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpPatch]
        [System.Web.Http.Route("api/Inventory/Publish/{uuid}")]
        public ServiceResult PublishItem(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
                return ServiceResponse.Error("You must provide an id for the item.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            var res = inventoryManager.Get(uuid);
            if (res.Code != 200)
                return res;
            InventoryItem p = (InventoryItem)res.Result;

            if (p.Deleted == true)
                return ServiceResponse.Error("Item cannote be published as it was deleted.");

            p.Published = true;
            return inventoryManager.Update(p);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Inventory/{name}")]
        public ServiceResult Search(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ServiceResponse.Error("You must provide a name for the item.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            List<InventoryItem> s = inventoryManager.Search(name);

            if (s == null || s.Count == 0)
                return ServiceResponse.Error("Inventory Item could not be located for the name " + name);

            return ServiceResponse.OK("", s);
        }

        //[ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        //[System.Web.Http.HttpPost]
        //[System.Web.Http.HttpGet]
        //[System.Web.Http.Route("api/Inventory")]
        //public ServiceResult GetItem()
        //{
        //    if (CurrentUser == null)
        //        return ServiceResponse.Error("You must be logged in to access this function.");

        //    InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
        //    List<dynamic> Inventory = (List<dynamic>)inventoryManager.GetItems(CurrentUser.AccountUUID).Cast<dynamic>().ToList();
        //  int count;

        //                     DataFilter filter = this.GetFilter(Request);
        //        Inventory = Inventory.Filter( tmp, filter);

        //    return ServiceResponse.OK("", Inventory, count);
        //}

        //[ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        //[System.Web.Http.HttpPost]
        //[System.Web.Http.HttpGet]
        //[System.Web.Http.Route("api/Inventory/Location/{locationUUID}")]
        //public ServiceResult GetItemsForLocation(string locationUUID)
        //{
        //    if (CurrentUser == null)
        //        return ServiceResponse.Error("You must be logged in to access this function.");

        //    InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
        //    List<dynamic> Inventory = (List<dynamic>)inventoryManager.GetItems(CurrentUser.AccountUUID).Cast<dynamic>().ToList();

        //    Inventory = Inventory.Where(w => w.LocationUUID == locationUUID &&
        //                        w.Deleted == false )
        //            .Cast<dynamic>().ToList();

        //    int count;

        //    DataFilter filter = this.GetFilter(Request);
        //    Inventory = Inventory.Filter( tmp, filter);
        //    return ServiceResponse.OK("", Inventory, count);
        //}

        //[System.Web.Http.HttpPost]
        //[System.Web.Http.HttpGet]
        //[System.Web.Http.Route("api/Inventory/{locationName}/distance/{distance}")]
        //public ServiceResult GetPublishedInventoryByLocation(string locationName, int distance)
        //{
        //    //if (CurrentUser == null)
        //   //     return ServiceResponse.Error("You must be logged in to access this function.");

        //    InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
        //    List<dynamic> Inventory = (List<dynamic>)inventoryManager.GetItems(locationName,distance ).Cast<dynamic>().ToList();  // && w.Expires && w.Private == false

        //    int count;

        //    DataFilter filter = this.GetFilter(Request);
        //    Inventory = Inventory.Filter(tmp, filter);
        //    return ServiceResponse.OK("", Inventory, count);
        //}

        //[System.Web.Http.HttpPost]
        //[System.Web.Http.HttpGet]
        //[System.Web.Http.Route("api/Inventory/Published")]
        //public ServiceResult GetPublishedInventory()
        //{
        //    //if (CurrentUser == null)
        //    //     return ServiceResponse.Error("You must be logged in to access this function.");

        //    InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
        //    List<dynamic> Inventory = (List<dynamic>)inventoryManager.GetPublishedItems().Cast<dynamic>().ToList();  // && w.Expires && w.Private == false

        //    int count;

        //    DataFilter filter = this.GetFilter(Request);
        //    Inventory = Inventory.Filter(tmp, filter);
        //    return ServiceResponse.OK("", Inventory, count);
        //}

        //[System.Web.Http.HttpPost]
        //[System.Web.Http.HttpGet]
        //[System.Web.Http.Route("api/Inventory/{locationName}/distance/{distance}/Search")]
        //public ServiceResult SearchPublishedInventory(string locationName, int distance)
        //{
        //    //if (CurrentUser == null)
        //    //     return ServiceResponse.Error("You must be logged in to access this function.");

        //    InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
        //    List<dynamic> Inventory = (List<dynamic>)inventoryManager.GetItems(locationName, distance).Cast<dynamic>().ToList();  // && w.Expires && w.Private == false

        //    int count;

        //    DataFilter filter = this.GetFilter(Request);

        //    if(tmpFilter == null || tmpFilter.Screens.Count == 0)
        //        return ServiceResponse.OK("", Inventory, Inventory.Count);

        //    Inventory = Inventory.Search(tmp, filter);

        //    tmpFilter.Screens = tmpFilter.Screens.Where(w => w.Command?.ToUpper() != "SEARCHBY" || w.Command?.ToUpper() != "SEARCH!BY").ToList();

        //    if ( tmpFilter.Screens.Count == 0)
        //        return ServiceResponse.OK("", Inventory, count);

        //    Inventory = Inventory.Filter(tmp, filter);
        //    return ServiceResponse.OK("", Inventory, count);
        //}
        //[ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        //[System.Web.Http.HttpGet]
        //[System.Web.Http.Route("api/Inventory/{inventoryItemUUID}/Images")]
        //public ServiceResult GetImageLinks(string inventoryItemUUID)
        //{
        //    if (CurrentUser == null)
        //        return ServiceResponse.Error("You must be logged in to access this function.");

        //    InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
        //    InventoryItem item = (InventoryItem)inventoryManager.Get(inventoryItemUUID);

        //    if (item == null )
        //        return ServiceResponse.Error("Invalid id was sent.");

        //    string root = System.Web.HttpContext.Current.Server.MapPath("~/Content/Uploads/" + this.CurrentUser.UUID);

        //    List<FileEx> files = new List<FileEx>();
        //    //get the default image, assigned to the object.
        //    FileEx file = new FileEx();
        //    file.UUID = item.UUID;
        //    file.UUIDType = item.UUIDType;
        //    file.Default = true;

        //    file.Status = "saved";
        //    file.Name = item.Image.GetFileNameFromUrl();
        //    file.Path = Path.Combine(root, file.Name);
        //    file.Image = item.Image;
        //    string fullUrl = this.Request.RequestUri.Scheme + "://" + this.Request.RequestUri.Authority + "/Content/Uploads/" + this.CurrentUser.UUID + "/";
        //    file.ImageThumb = fullUrl + ImageEx.GetThumbFileName(file.Path);
        //    files.Add(file);

        //    //Get secondary images assigned to the settings table.
        //    AppManager am = new AppManager(Globals.DBConnectionKey, "web", this.GetAuthToken(Request));

        //    List<Setting> settings = am.GetSettings(item.UUIDType)
        //        .Where(w => w.UUIDType.EqualsIgnoreCase("ImagePath") &&
        //               w.Value == item.UUID).ToList();

        //    foreach (Setting setting in settings)
        //    {
        //        file = new FileEx();

        //        file.UUID = setting.UUID;
        //        file.UUIDType = setting.UUIDType;
        //        file.Default = false;
        //        file.Status = "saved";
        //        file.Image = setting.Image;
        //        file.Name = file.Image.GetFileNameFromUrl();
        //        file.Path = Path.Combine(root, file.Name);
        //        file.ImageThumb = fullUrl + ImageEx.GetThumbFileName(file.Path);

        //        files.Add(file);
        //    }

        //    return ServiceResponse.OK("",files,files.Count);
        //}

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpPost]
        [System.Web.Http.HttpPatch]
        [System.Web.Http.Route("api/Inventory/Update")]
        public ServiceResult Update(InventoryItem pv)
        {
            if (CurrentUser == null)
                return ServiceResponse.Error("You must be logged in to access this function.");

            if (pv == null)
                return ServiceResponse.Error("Invalid location sent to server.");

            InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));
            var res = inventoryManager.Get(pv.UUID);
            if (res.Code != 200)
                return res;
            var dbP = (InventoryItem)res.Result;

            dbP.Name = pv.Name;
            dbP.Cost = pv.Cost;
            dbP.Price = pv.Price;
            dbP.UUIDType = pv.UUIDType;
            dbP.CategoryUUID = pv.CategoryUUID;
            dbP.Description = pv.Description;
            dbP.Condition = pv.Condition;
            dbP.Quality = pv.Quality;
            dbP.Deleted = pv.Deleted;
            dbP.Rating = pv.Rating;
            dbP.LocationUUID = pv.LocationUUID; //todo when updating itn the  client it's resetting it to non coordinate type
            dbP.LocationType = pv.LocationType; //todo when updating itn the  client it's resetting it to non coordinate type
            dbP.Quantity = pv.Quantity;
            dbP.ReferenceType = pv.ReferenceType;
            dbP.ReferenceUUID = pv.ReferenceUUID;
            dbP.Virtual = pv.Virtual;
            dbP.Published = pv.Published;
            dbP.Link = pv.Link;
            dbP.LinkProperties = pv.LinkProperties;
            //todo bookmark latest. image is saving as blank when doing the update. may need to requery for the item.
            dbP.Image = pv.Image;
            return inventoryManager.Update(dbP);
        }

        [ApiAuthorizationRequired(Operator = ">=", RoleWeight = 10)]
        [System.Web.Http.HttpPost]
        [System.Web.Http.HttpPatch]
        [System.Web.Http.Route("api/Inventory/Updates")]
        public ServiceResult UpdateInventory()
        {
            ServiceResult res = ServiceResponse.OK();
            StringBuilder msg = new StringBuilder();
            try
            {
                Task<string> content = Request.Content.ReadAsStringAsync();
                if (content == null)
                    return ServiceResponse.Error("No permissions were sent.");

                string body = content.Result;

                if (string.IsNullOrEmpty(body))
                    return ServiceResponse.Error("No permissions were sent.");

                List<InventoryItem> changedItems = JsonConvert.DeserializeObject<List<InventoryItem>>(body);

                InventoryManager inventoryManager = new InventoryManager(Globals.DBConnectionKey, this.GetAuthToken(Request));

                foreach (InventoryItem changedItem in changedItems)
                {
                    var res2 = inventoryManager.Get(changedItem.UUID);
                    if (res2.Code != 200)
                        continue;

                    var databaseItem = (InventoryItem)res2.Result;

                    if (string.IsNullOrWhiteSpace(changedItem.CreatedBy))
                        changedItem.CreatedBy = this.CurrentUser.UUID;

                    if (string.IsNullOrWhiteSpace(changedItem.AccountUUID))
                        changedItem.AccountUUID = this.CurrentUser.AccountUUID;

                    if (string.IsNullOrWhiteSpace(changedItem.UUID))
                        changedItem.UUID = Guid.NewGuid().ToString("N");

                    if (databaseItem == null)
                    {
                        changedItem.UUIDType = "InventoryItem";
                        changedItem.DateCreated = DateTime.UtcNow;

                        ServiceResult sr = inventoryManager.Insert(changedItem);
                        if (sr.Code != 200)
                        {
                            res.Code = 500;
                            msg.AppendLine(sr.Message);
                        }
                        continue;
                    }

                    databaseItem.Name = changedItem.Name;
                    databaseItem.Cost = changedItem.Cost;
                    databaseItem.Condition = changedItem.Condition;
                    databaseItem.Quality = changedItem.Quality;
                    databaseItem.Deleted = changedItem.Deleted;
                    databaseItem.Rating = changedItem.Rating;
                    databaseItem.LocationUUID = changedItem.LocationUUID;
                    databaseItem.LocationType = changedItem.LocationType;
                    databaseItem.Quantity = changedItem.Quantity;
                    databaseItem.ReferenceType = changedItem.ReferenceType;
                    databaseItem.ReferenceUUID = changedItem.ReferenceUUID;
                    databaseItem.Virtual = changedItem.Virtual;
                    databaseItem.Published = changedItem.Published;
                    databaseItem.Link = changedItem.Link;
                    databaseItem.LinkProperties = changedItem.LinkProperties;
                    if (CurrentUser.SiteAdmin)
                    {
                        databaseItem.RoleOperation = changedItem.RoleOperation;
                        databaseItem.RoleWeight = changedItem.RoleWeight;
                    }
                    ServiceResult sru = inventoryManager.Update(changedItem);
                    if (sru.Code != 200)
                    {
                        res.Code = 500;
                        msg.AppendLine(sru.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                msg.AppendLine(ex.Message);
                Debug.Assert(false, ex.Message);
            }
            res.Message = msg.ToString();
            return res;
        }
    }
}