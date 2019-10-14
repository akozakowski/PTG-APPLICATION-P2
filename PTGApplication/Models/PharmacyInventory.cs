//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PTGApplication.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class PharmacyInventory
    {
        public int Id { get; set; }
        public System.DateTime DateOrdered { get; set; }
        public string UserId { get; set; }
        public int BarcodeId { get; set; }
        public int StatusId { get; set; }
        public int CurrentLocationId { get; set; }
        public Nullable<int> FutureLocationId { get; set; }
        public System.DateTime ExpirationDate { get; set; }
    
        public virtual AspNetUser AspNetUser { get; set; }
        public virtual PharmacyDrug PharmacyDrug { get; set; }
        public virtual PharmacyLocation PharmacyLocation { get; set; }
        public virtual PharmacyLocation PharmacyLocation1 { get; set; }
        public virtual PharmacyStatu PharmacyStatu { get; set; }
    }
}