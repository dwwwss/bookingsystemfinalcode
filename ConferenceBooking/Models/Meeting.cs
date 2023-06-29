using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace ConferenceBooking.Models
{
    public class Meeting
    {
        internal object organizer_id;
        internal object room_id;
        public int RoomId { get; set; }
        [Required]
      
       
            
            public string RoomName { get; set; }
            public int Capacity { get; set; }
            public int MeetingId { get; set; }
        
     
            public int UserId { get; set; }


        public int meeting_id { get; set; }
        [Required]
        public string MeetingTitle { get; set; }
        public string participants { get; set; }
        [Required]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime StartTime { get; set; }
        [Required]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime EndTime { get; set; }
        [Required]
        public object AdditionalColumn1 { get; internal set; }
        public object AdditionalColumn2 { get; internal set; }
        public object AdditionalColumn3 { get; internal set; }
    
        public object OrganizerId { get; internal set; }
        public string OrganizerName { get; internal set; }
    }
}