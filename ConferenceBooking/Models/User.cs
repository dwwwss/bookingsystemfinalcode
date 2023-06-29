using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConferenceBooking.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string password { get; internal set; }
        public string name { get; set; }
        public string email { get; set; }
        public int user_id { get; internal set; }
        public bool isactive_status { get; internal set; }
        public string position { get; internal set; }
        public int UserId { get; internal set; }
    }
}