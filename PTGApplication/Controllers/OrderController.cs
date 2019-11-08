﻿using Microsoft.AspNet.Identity;
using PTGApplication.Models;
using PTGApplication.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace PTGApplication.Controllers
{
    public class OrderController : Controller
    {
        // GET: Order
        public ActionResult Index()
        {
            return View();
        }

        // GET: Order/PlaceOrder
        public ActionResult PlaceOrder(int id, int qty)
        {
            using (var uzima = new UzimaRxEntities())
            {
                ViewBag.Quantity = qty;
                var inventory = (from i in uzima.UzimaDrugs where i.Id == id select i).Single();
                ViewBag.Drug = inventory;

                var locations = uzima.UzimaLocations.ToList();
                var users = uzima.AspNetUsers.ToList();
                if (!(locations is null))
                {

                    var userhomelocation = (User.IsInRole(Properties.UserRoles.PharmacyManager)) ?
                        (from location in locations select location) :
                       (from location in locations
                        join user in users on location.LocationName equals user.HomePharmacy
                        where user.Username == User.Identity.Name
                        select location);

                    ViewBag.LocationNeeded = new SelectList(userhomelocation, "Id", "LocationName");
                }

                return View();
            }
        }
        // POST: Order/PlaceOrder
        [HttpPost]
        public async Task<ActionResult> PlaceOrder(String drugname, String txtQty, UzimaInventory model)
        {

            int id;
            string userid;

            try
            {
                using (var uzima = new UzimaRxEntities())
                {
                    userid =
                        (from user in uzima.AspNetUsers
                         where user.Username == User.Identity.Name
                         select user.Id).SingleOrDefault();
                    for (int i = 0; i < Convert.ToInt32(txtQty); i++)
                    {

                        id =
                            (from drug in uzima.UzimaInventories
                             join location in uzima.UzimaLocationTypes on drug.CurrentLocationId equals location.LocationId
                             where location.Supplier == null && drug.StatusId == 0 && drugname == drug.UzimaDrug.DrugName
                             select drug.Id).FirstOrDefault();


                        var entryToEdit = uzima.UzimaInventories.Find(id);
                        uzima.UzimaInventories.Remove(entryToEdit);
                        await uzima.SaveChangesAsync();

                        entryToEdit.FutureLocationId = model.FutureLocationId;
                        entryToEdit.DateOrdered = DateTime.Now;
                        entryToEdit.LastModifiedBy = userid;
                        entryToEdit.StatusId = 1;


                        uzima.UzimaInventories.Add(entryToEdit);
                        await uzima.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException is null)
                {
                    ViewBag.errorMessage = ex.Message;
                }
                else
                {
                    ViewBag.errorMessage = "Something went wrong internally.";
                }

                return View("Error");
            }

            return RedirectToAction("OrderPlaced");
        }

        // GET: Order/SelectPlaceOrder
        public ActionResult SelectPlaceOrder()
        {
            AspNetUser user;
            UzimaLocationType homePharmacy;
            UzimaLocation homeSupplier;
            IEnumerable<UzimaInventory> inventories;
            using (var uzima = new UzimaRxEntities())
            {
                var userId = User.Identity.GetUserId();
                user = (from u in uzima.AspNetUsers where u.Id == userId select u).Single();
                homePharmacy = (from lt in uzima.UzimaLocationTypes join l in uzima.UzimaLocations on lt.LocationId equals l.Id where l.LocationName == user.HomePharmacy select lt).Single();
                homeSupplier = User.IsInRole(Properties.UserRoles.PharmacyManager) ?
                    (from l in uzima.UzimaLocations where l.Id == homePharmacy.Id select l).Single()
                   : (from l in uzima.UzimaLocations
                      join lt in uzima.UzimaLocationTypes on l.Id equals lt.LocationId
                      where lt.LocationId == homePharmacy.Supplier
                      select l).Single();
                inventories = User.IsInRole(Properties.UserRoles.PharmacyManager)
                    ? uzima.UzimaInventories.ToList() :
                    (from inventory in uzima.UzimaInventories
                     join locationtype in uzima.UzimaLocationTypes on inventory.CurrentLocationId equals homeSupplier.Id
                     where inventory.StatusId == 0 && inventory.FutureLocationId == null && locationtype.Supplier == homeSupplier.Id
                     select inventory).ToList();

                var query = User.IsInRole(Properties.UserRoles.PharmacyManager) ?
                    "SELECT [UzimaDrug].Id, [UzimaDrug].DrugName AS 'Drug Name', COUNT(*) AS Quantity, [UzimaInventory].ExpirationDate AS 'Expiration Date' " +
                    "FROM UzimaInventory LEFT JOIN [UzimaDrug] ON [UzimaDrug].Id=[UzimaInventory].DrugId " +
                    "WHERE [UzimaInventory].StatusId=0 AND [UzimaInventory].FutureLocationId IS NULL " +
                    "GROUP BY [UzimaDrug].Id, [UzimaDrug].DrugName, [UzimaInventory].ExpirationDate" :
                    "SELECT [UzimaDrug].Id, [UzimaDrug].DrugName AS 'Drug Name', COUNT(*) AS Quantity, [UzimaInventory].ExpirationDate AS 'Expiration Date' " +
                    "FROM UzimaInventory LEFT JOIN [UzimaDrug] ON [UzimaDrug].Id=[UzimaInventory].DrugId " +
                    $"WHERE [UzimaInventory].StatusId=0 AND [UzimaInventory].CurrentLocationId={homeSupplier.Id} AND [UzimaInventory].FutureLocationId IS NULL " +
                    "GROUP BY [UzimaDrug].Id, [UzimaDrug].DrugName, [UzimaInventory].ExpirationDate";
                using (var dataSet = ConnectionPool.Query(query, "UzimaDrug", "UzimaInventory"))
                {
                    ViewBag.Columns = dataSet.Tables[0].Columns;
                    ViewBag.Data = dataSet.Tables[0].Rows;
                    return View();
                }
            }
        }

        //GET: Order/OrderPlaced
        public ActionResult OrderPlaced()
        {
            return View();
        }



        // GET: Order/SendOrder
        public ActionResult SendOrder()
        {
            IEnumerable<UzimaDrug> drugs;
            IEnumerable<UzimaLocation> locations;
            IEnumerable<UzimaInventory> inventories;
            IEnumerable<UzimaLocationType> locationTypes;
            using (var uzima = new UzimaRxEntities())
            {
                drugs = uzima.UzimaDrugs.ToList();
                locations = uzima.UzimaLocations.ToList();
                inventories = uzima.UzimaInventories.ToList();
                locationTypes = uzima.UzimaLocationTypes.ToList();
            }

            if (!(drugs is null) && !(inventories is null))
            {
                var inventorydrugs =
                    (from drug in drugs
                     join inventory in inventories on drug.Id equals inventory.DrugId
                     where drug.Id == inventory.DrugId && inventory.StatusId == 1
                     select drug
                     );

                ViewBag.Drugs = new SelectList(inventorydrugs.Distinct(), "Id", "DrugName");

                if (inventorydrugs.Count() == 0)
                {

                    ViewBag.Message = "There are currently no orders to be sent.";
                    return View("Info");
                }
            }

            if (!(locations is null) && !(locationTypes is null))
            {
                var clinics =
                    (from location in locations
                     join type in locationTypes on location.Id equals type.LocationId
                     where type.Supplier != null
                     select location);

                ViewBag.LocationNeeded = new SelectList(clinics, "Id", "LocationName");
            }
            return View();
        }


        // POST: Order/SendOrder
        [HttpPost]
        public async Task<ActionResult> SendOrder(String txtQty, UzimaInventory model)
        {

            int id;
            string userid;

            try
            {
                using (var uzima = new UzimaRxEntities())
                {
                    userid =
                        (from user in uzima.AspNetUsers
                         where user.Username == User.Identity.Name
                         select user.Id).SingleOrDefault();
                    for (int i = 0; i < Convert.ToInt32(txtQty); i++)
                    {

                        id =
                            (from drug in uzima.UzimaInventories
                             join location in uzima.UzimaLocationTypes on drug.CurrentLocationId equals location.LocationId
                             where location.Supplier == null && drug.StatusId == 1 && model.DrugId == drug.DrugId
                             select drug.Id).FirstOrDefault();


                        var entryToEdit = uzima.UzimaInventories.Find(id);
                        uzima.UzimaInventories.Remove(entryToEdit);
                        await uzima.SaveChangesAsync();

                        entryToEdit.FutureLocationId = model.FutureLocationId;
                        entryToEdit.DateOrdered = DateTime.Now;
                        entryToEdit.LastModifiedBy = userid;
                        entryToEdit.StatusId = 2;


                        uzima.UzimaInventories.Add(entryToEdit);
                        await uzima.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException is null)
                {
                    ViewBag.errorMessage = ex.Message;
                }
                else
                {
                    ViewBag.errorMessage = "Something went wrong internally.";
                }

                return View("Error");
            }

            return RedirectToAction("OrderSent");
        }

        // GET: OrderSent
        public ActionResult OrderSent()
        {
            return View();
        }

        // GET: Order/RecieveOrder
        public ActionResult RecieveOrder()
        {
            IEnumerable<UzimaDrug> drugs;
            IEnumerable<UzimaLocation> locations;
            IEnumerable<UzimaInventory> inventories;
            IEnumerable<UzimaLocationType> locationTypes;
            IEnumerable<AspNetUser> users;

            using (var uzima = new UzimaRxEntities())
            {
                drugs = uzima.UzimaDrugs.ToList();
                locations = uzima.UzimaLocations.ToList();
                inventories = uzima.UzimaInventories.ToList();
                locationTypes = uzima.UzimaLocationTypes.ToList();
                users = uzima.AspNetUsers.ToList();
            }

            if (!(drugs is null) && !(inventories is null))
            {

                var userhomelocation =
                    (from location in locations
                     join user in users on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location.Id).SingleOrDefault();

                var inventorydrugs =
                    (from drug in drugs
                     join inventory in inventories on drug.Id equals inventory.DrugId
                     join location in locations on inventory.FutureLocationId equals location.Id
                     join user in users on location.LocationName equals user.HomePharmacy
                     where drug.Id == inventory.DrugId && inventory.StatusId == 2 && inventory.FutureLocationId == userhomelocation
                     select drug
                     );

                ViewBag.Drugs = new SelectList(inventorydrugs.Distinct(), "Id", "DrugName");

                if (inventorydrugs.Count() == 0)
                {

                    ViewBag.Message = "You have no drugs on order to be recieved.";
                    return View("Info");
                }
            }

            if (!(locations is null) && !(locationTypes is null))
            {
                var userhomelocation =
                    (from location in locations
                     join user in users on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location);

                ViewBag.LocationRecieved = new SelectList(userhomelocation, "Id", "LocationName");
            }
            return View();
        }


        // POST: Order/RecieveOrder
        [HttpPost]
        public async Task<ActionResult> RecieveOrder(String txtQty, UzimaInventory model)
        {

            int id;
            string userid;

            try
            {
                using (var uzima = new UzimaRxEntities())
                {

                    var userhomelocation =
                    (from location in uzima.UzimaLocations
                     join user in uzima.AspNetUsers on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location.Id).SingleOrDefault();

                    userid =
                        (from user in uzima.AspNetUsers
                         where user.Username == User.Identity.Name
                         select user.Id).SingleOrDefault();
                    for (int i = 0; i < Convert.ToInt32(txtQty); i++)
                    {

                        id =
                            (from drug in uzima.UzimaInventories
                             join location in uzima.UzimaLocationTypes on drug.CurrentLocationId equals location.LocationId
                             where drug.StatusId == 2 && model.DrugId == drug.DrugId && drug.FutureLocationId == userhomelocation
                             select drug.Id).FirstOrDefault();


                        var entryToEdit = uzima.UzimaInventories.Find(id);
                        uzima.UzimaInventories.Remove(entryToEdit);
                        await uzima.SaveChangesAsync();

                        entryToEdit.FutureLocationId = model.FutureLocationId;
                        entryToEdit.CurrentLocationId = (int)model.FutureLocationId;
                        entryToEdit.DateOrdered = DateTime.Now;
                        entryToEdit.LastModifiedBy = userid;
                        entryToEdit.StatusId = 0;


                        uzima.UzimaInventories.Add(entryToEdit);
                        await uzima.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException is null)
                {
                    ViewBag.errorMessage = ex.Message;
                }
                else
                {
                    ViewBag.errorMessage = "Something went wrong internally.";
                }

                return View("Error");
            }

            return RedirectToAction("RecieveOrder");
        }

        // GET: Order/DispenseItem
        public ActionResult DispenseItem()
        {
            IEnumerable<UzimaDrug> drugs;
            IEnumerable<UzimaLocation> locations;
            IEnumerable<UzimaInventory> inventories;
            IEnumerable<UzimaLocationType> locationTypes;
            IEnumerable<AspNetUser> users;

            using (var uzima = new UzimaRxEntities())
            {
                drugs = uzima.UzimaDrugs.ToList();
                locations = uzima.UzimaLocations.ToList();
                inventories = uzima.UzimaInventories.ToList();
                locationTypes = uzima.UzimaLocationTypes.ToList();
                users = uzima.AspNetUsers.ToList();
            }

            if (!(drugs is null) && !(inventories is null))
            {

                var userhomelocation =
                    (from location in locations
                     join user in users on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location.Id).SingleOrDefault();

                var inventorydrugs =
                    (from drug in drugs
                     join inventory in inventories on drug.Id equals inventory.DrugId
                     join location in locations on inventory.FutureLocationId equals location.Id
                     join user in users on location.LocationName equals user.HomePharmacy
                     where drug.Id == inventory.DrugId && inventory.StatusId == 0 && inventory.FutureLocationId == userhomelocation
                     select drug
                     );

                ViewBag.Drugs = new SelectList(inventorydrugs.Distinct(), "Id", "DrugName");

                if (inventorydrugs.Count() == 0)
                {

                    ViewBag.Message = "You have no items in inventory to dispense. (Please input item as recieved before dispensing.)";
                    return View("Info");
                }
            }

            if (!(locations is null) && !(locationTypes is null))
            {
                var userhomelocation =
                    (from location in locations
                     join user in users on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location);

                ViewBag.LocationDispensed = new SelectList(userhomelocation, "Id", "LocationName");
            }
            return View();
        }


        // POST: Order/DispenseItem
        [HttpPost]
        public async Task<ActionResult> DispenseItem(String txtQty, UzimaInventory model)
        {

            int id;
            string userid;

            try
            {
                using (var uzima = new UzimaRxEntities())
                {

                    var userhomelocation =
                    (from location in uzima.UzimaLocations
                     join user in uzima.AspNetUsers on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location.Id).SingleOrDefault();

                    userid =
                        (from user in uzima.AspNetUsers
                         where user.Username == User.Identity.Name
                         select user.Id).SingleOrDefault();
                    for (int i = 0; i < Convert.ToInt32(txtQty); i++)
                    {

                        id =
                            (from drug in uzima.UzimaInventories
                             where drug.StatusId == 0 && model.DrugId == drug.DrugId && drug.FutureLocationId == userhomelocation
                             select drug.Id).FirstOrDefault();


                        var entryToEdit = uzima.UzimaInventories.Find(id);
                        uzima.UzimaInventories.Remove(entryToEdit);
                        await uzima.SaveChangesAsync();

                        entryToEdit.FutureLocationId = model.FutureLocationId;
                        entryToEdit.CurrentLocationId = (int)model.FutureLocationId;
                        entryToEdit.DateOrdered = DateTime.Now;
                        entryToEdit.LastModifiedBy = userid;
                        entryToEdit.StatusId = 3;


                        uzima.UzimaInventories.Add(entryToEdit);
                        await uzima.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException is null)
                {
                    ViewBag.errorMessage = ex.Message;
                }
                else
                {
                    ViewBag.errorMessage = "Something went wrong internally.";
                }

                return View("Error");
            }

            return RedirectToAction("DispenseItem");
        }


        // GET: Order/DestroyItem
        public ActionResult DestroyItem()
        {
            IEnumerable<UzimaDrug> drugs;
            IEnumerable<UzimaLocation> locations;
            IEnumerable<UzimaInventory> inventories;
            IEnumerable<UzimaLocationType> locationTypes;
            IEnumerable<AspNetUser> users;

            using (var uzima = new UzimaRxEntities())
            {
                drugs = uzima.UzimaDrugs.ToList();
                locations = uzima.UzimaLocations.ToList();
                inventories = uzima.UzimaInventories.ToList();
                locationTypes = uzima.UzimaLocationTypes.ToList();
                users = uzima.AspNetUsers.ToList();
            }

            if (!(drugs is null) && !(inventories is null))
            {

                var userhomelocation =
                    (from location in locations
                     join user in users on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location.Id).SingleOrDefault();

                var inventorydrugs =
                    (from drug in drugs
                     join inventory in inventories on drug.Id equals inventory.DrugId
                     join location in locations on inventory.FutureLocationId equals location.Id
                     join user in users on location.LocationName equals user.HomePharmacy
                     where drug.Id == inventory.DrugId && inventory.StatusId == 0 && inventory.FutureLocationId == userhomelocation
                     select drug
                     );

                ViewBag.Drugs = new SelectList(inventorydrugs.Distinct(), "Id", "DrugName");

                if (inventorydrugs.Count() == 0)
                {

                    ViewBag.Message = "You have no items in inventory to destroy. (If items were destroyed in transit, please input as recieved and then destroy.)";
                    return View("Info");
                }
            }

            if (!(locations is null) && !(locationTypes is null))
            {
                var userhomelocation =
                    (from location in locations
                     join user in users on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location);

                ViewBag.LocationDestroyed = new SelectList(userhomelocation, "Id", "LocationName");
            }
            return View();
        }


        // POST: Order/DestroyOrder
        [HttpPost]
        public async Task<ActionResult> DestroyItem(String txtQty, UzimaInventory model)
        {

            int id;
            string userid;

            try
            {
                using (var uzima = new UzimaRxEntities())
                {

                    var userhomelocation =
                    (from location in uzima.UzimaLocations
                     join user in uzima.AspNetUsers on location.LocationName equals user.HomePharmacy
                     where user.Username == User.Identity.Name
                     select location.Id).SingleOrDefault();

                    userid =
                        (from user in uzima.AspNetUsers
                         where user.Username == User.Identity.Name
                         select user.Id).SingleOrDefault();
                    for (int i = 0; i < Convert.ToInt32(txtQty); i++)
                    {

                        id =
                            (from drug in uzima.UzimaInventories
                             where drug.StatusId == 0 && model.DrugId == drug.DrugId && drug.FutureLocationId == userhomelocation
                             select drug.Id).FirstOrDefault();


                        var entryToEdit = uzima.UzimaInventories.Find(id);
                        uzima.UzimaInventories.Remove(entryToEdit);
                        await uzima.SaveChangesAsync();

                        entryToEdit.FutureLocationId = model.FutureLocationId;
                        entryToEdit.CurrentLocationId = (int)model.FutureLocationId;
                        entryToEdit.DateOrdered = DateTime.Now;
                        entryToEdit.LastModifiedBy = userid;
                        entryToEdit.StatusId = 4;


                        uzima.UzimaInventories.Add(entryToEdit);
                        await uzima.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException is null)
                {
                    ViewBag.errorMessage = ex.Message;
                }
                else
                {
                    ViewBag.errorMessage = "Something went wrong internally.";
                }

                return View("Error");
            }

            return RedirectToAction("DestroyItem");
        }

    }
}
