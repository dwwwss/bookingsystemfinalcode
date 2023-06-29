using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Web.Mvc;
using ConferenceBooking.Models;
using Microsoft.Graph.Models;
using Newtonsoft.Json;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Web.Http.Cors;

namespace ConferenceBooking.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class HomeController : Controller
    {
        private readonly string _connectionString;

        public HomeController()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString;
        }

        public ActionResult Index()
        {
            List<Meeting> meetings = new List<Meeting>();
            ViewBag.ErrorMessage = TempData["ErrorMessage"] as string;

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT * FROM Meetings";
                SqlCommand command = new SqlCommand(query, connection);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Meeting meeting = new Meeting
                    {
                        MeetingId = reader["meeting_id"] != DBNull.Value ? (int)reader["meeting_id"] : 0,
                        MeetingTitle = reader["meeting_title"] != DBNull.Value ? (string)reader["meeting_title"] : string.Empty,
                        StartTime = reader["start_time"] != DBNull.Value ? (DateTime)reader["start_time"] : DateTime.MinValue,
                        EndTime = reader["end_time"] != DBNull.Value ? (DateTime)reader["end_time"] : DateTime.MinValue,
                        RoomId = reader["room_id"] != DBNull.Value ? (int)reader["room_id"] : 0,
                    };

                    meetings.Add(meeting);
                }

                reader.Close();
            }

            // Retrieve the list of rooms and pass it to the view
            List<Room> rooms = GetRoomsFromDatabase();
            ViewBag.Rooms = rooms;

            return View(meetings);
        }
        private List<Room> GetRoomsFromDatabase()
        {
            List<Room> rooms = new List<Room>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT * FROM Rooms";
                SqlCommand command = new SqlCommand(query, connection);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Room room = new Room
                    {
                        RoomId = reader["room_id"] != DBNull.Value ? (int)reader["room_id"] : 0,
                        RoomName = reader["room_name"] != DBNull.Value ? (string)reader["room_name"] : string.Empty,
                        Capacity = reader["capacity"] != DBNull.Value ? (int)reader["capacity"] : 0,
                    };

                    rooms.Add(room);
                }

                reader.Close();
            }

            return rooms;
        }
        public class User
        {
            public int UserId { get; set; }
            public string UserName { get; set; }
            public string position { get; set; }
            public string Position { get; internal set; }
            public string email { get; set; }
            public string Email { get; internal set; }
        }
        public class Room
        {
            public int MeetingId { get; set; }
            public string MeetingTitle { get; set; }
            public string RoomCode { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public int RoomId { get; set; } // Updated property name to match casing
            public int organizer_id { get; set; }
            public int UserId { get; set; }
            public string RoomName { get; set; }
            public int? Capacity { get; set; }

        }
        public class Meeting
        {
            public int MeetingId { get; set; }
            public string MeetingTitle { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public int RoomId { get; set; } // Updated property name to match casing
            public int organizer_id { get; set; }
            public int UserId { get; set; }
            public object RoomName { get; set; }
            public int? Capacity { get; internal set; }
            public string BookedBy { get; internal set; }
        }
        [HttpPost]
        public ActionResult BookMeeting(Meeting meeting)
        {
            // Check if the same time slot already exists
            bool isSlotAvailable = IsTimeSlotAvailable(meeting.StartTime, meeting.EndTime, meeting.RoomId);
            if (!isSlotAvailable)
            {
                ModelState.Clear(); // Clear all previous model state errors
                TempData["ErrorMessage"] = "The selected time slot is not available. Please choose a different time slot.";

                // Redirect to the index page
                return RedirectToAction("Index", "Home");
            }

            // Retrieve the room details from the database
            Room selectedRoom = GetRoomById(meeting.RoomId);
            meeting.RoomName = selectedRoom.RoomName;
            meeting.Capacity = selectedRoom.Capacity;

            // Retrieve the latest meeting_id from the database
            int latestMeetingId;
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT ISNULL(MAX(meeting_id), 0) FROM Meetings";
                SqlCommand command = new SqlCommand(query, connection);

                connection.Open();
                latestMeetingId = (int)command.ExecuteScalar() + 1;
            }

            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int userId))
            {
                meeting.UserId = userId; // Set the UserId property to the user ID
            }
            else
            {
                // Handle the case when the user ID is not found in the session or cannot be parsed as an integer
                // You can redirect the user to the login page or display an error message
            }

            // Insert the meeting into the database
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "INSERT INTO Meetings (meeting_id, meeting_title, start_time, end_time, room_id, user_id) VALUES (@MeetingId, @MeetingTitle, @StartTime, @EndTime, @RoomId, @UserId)";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@MeetingId", latestMeetingId);
                command.Parameters.AddWithValue("@MeetingTitle", meeting.MeetingTitle);
                command.Parameters.AddWithValue("@StartTime", meeting.StartTime);
                command.Parameters.AddWithValue("@EndTime", meeting.EndTime);
                command.Parameters.AddWithValue("@RoomId", meeting.RoomId);
                command.Parameters.AddWithValue("@UserId", meeting.UserId); // Add the UserId parameter

                connection.Open();
                command.ExecuteNonQuery();
            }

            string userEmail = Session["UserEmail"]?.ToString();

            if (userEmail != null && userEmail.ToLower() == "dpatidar1221@gmail.com")
            {
                // Redirect to the admin page for the specified email
                return RedirectToAction("adminpage", "Home");
            }
            else
            {
                // Redirect to the index page for other email IDs
                return RedirectToAction("Index", "Home");
            }
        }

        private Room GetRoomById(int roomId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT room_id, room_name, capacity FROM Rooms WHERE room_id = @RoomId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@RoomId", roomId);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                if (reader.Read())
                {
                    Room room = new Room
                    {
                        RoomId = (int)reader["room_id"],
                        RoomName = (string)reader["room_name"],
                        Capacity = (int)reader["capacity"]
                    };

                    reader.Close();
                    return room;
                }
            }

            return null;
        }



        private List<Meeting> GetMeetingsFromDatabase()
        {
            List<Meeting> meetings = new List<Meeting>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT m.meeting_id, m.meeting_title, m.start_time, m.end_time, m.room_id, u.name AS booked_by FROM Meetings m INNER JOIN Users u ON m.user_id = u.user_id";
                SqlCommand command = new SqlCommand(query, connection);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Meeting meeting = new Meeting();
                    meeting.MeetingId = (int)reader["meeting_id"];
                    meeting.MeetingTitle = (string)reader["meeting_title"];
                    meeting.StartTime = (DateTime)reader["start_time"];
                    meeting.EndTime = (DateTime)reader["end_time"];
                    meeting.RoomId = (int)reader["room_id"];
                    meeting.BookedBy = (string)reader["booked_by"]; // Assuming the user name column is named "name" in the Users table

                    meetings.Add(meeting);
                }

                reader.Close();
            }

            return meetings;
        }


        private bool IsTimeSlotAvailable(DateTime startTime, DateTime endTime, int roomId)
        {
            bool isAvailable = true;

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT COUNT(*) FROM Meetings WHERE room_id = @RoomId AND (@StartTime < end_time AND @EndTime > start_time)";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@RoomId", roomId);
                command.Parameters.AddWithValue("@StartTime", startTime);
                command.Parameters.AddWithValue("@EndTime", endTime);

                connection.Open();
                int conflictingMeetingsCount = (int)command.ExecuteScalar();

                if (conflictingMeetingsCount > 0)
                {
                    isAvailable = false;
                }
            }

            return isAvailable;
        }

        [HttpPost]
        public ActionResult DeleteMeeting(int id)
        {
            // Check if the user is authorized to delete the meeting
            int userId = GetLoggedInUserId();
            bool isAuthorized = IsMeetingCreatedByUser(id, userId);

            if (!isAuthorized)
            {
                string errorMessage = "You are not authorized to delete this meeting.";
                TempData["ErrorMessage"] = errorMessage; // Store the error message in TempData

                return RedirectToAction("Index", "Home"); // Redirect to the index page
            }

            // Proceed with deleting the meeting
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "DELETE FROM Meetings WHERE meeting_id = @MeetingId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@MeetingId", id);

                connection.Open();
                command.ExecuteNonQuery();
            }

            // Optionally, you can store a success message in TempData
            TempData["SuccessMessage"] = "Meeting deleted successfully";

            return RedirectToAction("Index", "Home"); // Redirect to the index page
        }


        private int GetLoggedInUserId()
        {
            // Implement your logic to get the logged-in user's ID
            // For example, retrieve it from the session or any other authentication mechanism
            // Replace this with your actual implementation
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int userId))
            {
                return userId;
            }

            return 0;
        }

        private bool IsMeetingCreatedByUser(int meetingId, int userId)
        {
            // Implement your logic to check if the meeting with the given ID was created by the specified user
            // For example, you can query the database to check if the meeting's user_id matches the specified user ID
            // Replace this with your actual implementation
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT COUNT(*) FROM Meetings WHERE meeting_id = @MeetingId AND user_id = @UserId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@MeetingId", meetingId);
                command.Parameters.AddWithValue("@UserId", userId);

                connection.Open();
                int matchingMeetingsCount = (int)command.ExecuteScalar();

                return matchingMeetingsCount > 0;
            }
        }
        [HttpPost]
        public ActionResult EditMeeting(Meeting meeting)
        {
            // Check if the user is authorized to edit the meeting
            int userId = GetLoggedInUserId();
            bool isAuthorized = IsMeetingCreatedByUser(meeting.MeetingId, userId);

            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "You are not authorized to edit this meeting.";
                return RedirectToAction("Index");
            }

            try
            {
                Room selectedRoom = GetRoomById(meeting.RoomId);
                if (selectedRoom == null)
                {
                    ViewBag.ErrorMessage = "Selected room not found.";
                    return RedirectToAction("Index");
                }

                meeting.RoomName = selectedRoom.RoomName;
                meeting.Capacity = selectedRoom.Capacity;

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    string query = "UPDATE Meetings SET meeting_title = @Title, start_time = @StartTime, end_time = @EndTime, room_id = @RoomId WHERE meeting_id = @MeetingId";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Title", meeting.MeetingTitle);
                    command.Parameters.AddWithValue("@StartTime", meeting.StartTime);
                    command.Parameters.AddWithValue("@EndTime", meeting.EndTime);
                    command.Parameters.AddWithValue("@RoomId", meeting.RoomId);
                    command.Parameters.AddWithValue("@MeetingId", meeting.MeetingId);

                    connection.Open();
                    command.ExecuteNonQuery();
                }

                ViewBag.AlertMessage = "Meeting updated successfully.";
            }
            catch (Exception ex)
            {
                // Handle the exception, log error, or display an error message
                ViewBag.ErrorMessage = ex.Message;
            }

            return RedirectToAction("Index");
        }


        [HttpPost]
        public ActionResult Login(Loginmodel model)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    string query = "SELECT user_id, email, password, isactive_status FROM Users WHERE email = @Email AND password = @Password";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Email", model.Username);
                    command.Parameters.AddWithValue("@Password", model.Password);

                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        reader.Read();
                        var userId = reader.GetInt32(reader.GetOrdinal("user_id"));
                        var isActive = reader.GetBoolean(reader.GetOrdinal("isactive_status"));

                        var user = new Models.User
                        {
                            UserId = userId,
                            Username = reader.GetString(reader.GetOrdinal("email")),
                            Password = reader.GetString(reader.GetOrdinal("password")),
                            // Assign other properties as needed
                        };

                        // Perform any other necessary authentication or authorization steps here

                        if (isActive)
                        {
                            // Store the user ID and email in the session
                            Session["UserId"] = userId;
                            Session["UserEmail"] = model.Username; // Store the user email

                            // Redirect to the admin page
                            return RedirectToAction("adminpage", "Home");
                        }
                        else
                        {
                            // Store the user ID and email in the session
                            Session["UserId"] = userId;
                            Session["UserEmail"] = model.Username; // Store the user email

                            // Redirect to the index page
                            return RedirectToAction("Index", "Home");
                        }

                    }
                    else
                    {
                        // Invalid credentials
                        ModelState.AddModelError("", "Invalid username or password");
                    }
                }
            }

            // If there are validation errors or login fails, return the view with the model
            return View(model);
        }



        [HttpGet]
        public ActionResult login()
        {

            return View();
        }
        [HttpGet]
        public ActionResult Meetings()
        {
            List<Meeting> meetings = GetMeetingsFromDatabase();
            return View(meetings);
        }

        [HttpGet]
        public ActionResult Register()
        {
            // Populate the list of positions and assign it to the ViewBag
            var positionList = GetPositionList();
            ViewBag.PositionList = positionList;

            return View();
        }
        [HttpPost]
        public ActionResult Register(Regis model)
        {
            if (ModelState.IsValid)
            {
                // Check if the email already exists
                using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Users WHERE email = @Email";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Email", model.Email);
                    int existingUserCount = (int)command.ExecuteScalar();
                    if (existingUserCount > 0)
                    {
                        ModelState.AddModelError("", "Email already exists. Please use a different email.");
                        ViewBag.PositionList = GetPositionList(); // Populate the position list again
                        ViewBag.ErrorMessage = "Email already exists. Please use a different email."; // Set the error message in ViewBag
                        return View(model);
                    }
                }

                try
                {
                    // Generate a unique user ID (incremented by one from the last user ID)
                    int lastUserId;
                    using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString))
                    {
                        connection.Open();
                        string query = "SELECT ISNULL(MAX(user_id), 0) FROM Users";
                        SqlCommand command = new SqlCommand(query, connection);
                        lastUserId = (int)command.ExecuteScalar();
                    }
                    int newUserId = lastUserId + 1;

                    // Create a new User object with the registration data
                    Models.User newUser = new Models.User
                    {
                        user_id = newUserId,
                        name = model.Name,
                        email = model.Email,
                        password = model.Password,
                        position = model.Position,
                        isactive_status = model.IsActiveStatus
                    };

                    // Save the new user to the database
                    using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString))
                    {
                        connection.Open();
                        string query = "INSERT INTO Users (user_id, name, email, password, position, isactive_status) VALUES (@UserId, @Name, @Email, @Password, @Position, @IsActiveStatus)";
                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@UserId", newUser.user_id);
                        command.Parameters.AddWithValue("@Name", newUser.name);
                        command.Parameters.AddWithValue("@Email", newUser.email);
                        command.Parameters.AddWithValue("@Password", newUser.password);
                        command.Parameters.AddWithValue("@Position", newUser.position);
                        command.Parameters.AddWithValue("@IsActiveStatus", newUser.isactive_status);
                        command.ExecuteNonQuery();
                    }

                    // Redirect to the login page after successful registration
                    return RedirectToAction("Login", "Home");
                }
                catch (Exception ex)
                {
                    // Handle database error
                    ModelState.AddModelError("", "Registration failed. Please try again later.");
                }
            }

            // Populate the list of positions and assign it to the ViewBag
            ViewBag.PositionList = GetPositionList();

            // If there are validation errors or the registration fails, return the view with the model
            return View(model);
        }

        private List<SelectListItem> GetPositionList()
        {
            return new List<SelectListItem>
    {
        new SelectListItem { Value = "Trainee", Text = "Trainee" },
        new SelectListItem { Value = "Senior Software Engineer", Text = "Senior Software Engineer" },
        new SelectListItem { Value = "Intern", Text = "Intern" },
        new SelectListItem { Value = "HR", Text = "HR" },
        new SelectListItem { Value = "Management", Text = "Management" }
        // Add more positions as needed
    };
        }

    
        [HttpPost]
        public ActionResult Adduser(RegistrationModel model)
        {
            if (ModelState.IsValid)
            {
                if (IsEmailAlreadyExists(model.Email))
                {
                    ModelState.AddModelError("", "Email already exists. Please use a different email.");
                }
                else
                {
                    try
                    {
                        int newUserId = GenerateNewUserId();
                        Models.User newUser = CreateUserObject(model, newUserId);
                        SaveUserToDatabase(newUser);

                        return RedirectToAction("adminpage");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Registration failed. Please try again later.");
                    }
                }
            }

            ViewBag.PositionList = GetPositionList1();

            return View("adminpage", model);
        }

        private bool IsEmailAlreadyExists(string email)
        {
            using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM Users WHERE email = @Email";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Email", email);
                int existingUserCount = (int)command.ExecuteScalar();
                return existingUserCount > 0;
            }
        }

        private int GenerateNewUserId()
        {
            using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString))
            {
                connection.Open();
                string query = "SELECT ISNULL(MAX(user_id), 0) FROM Users";
                SqlCommand command = new SqlCommand(query, connection);
                return (int)command.ExecuteScalar() + 1;
            }
        }

        private Models.User CreateUserObject(RegistrationModel model, int newUserId)
        {
            return new Models.User
            {
                user_id = newUserId,
                name = model.Name,
                email = model.Email,
                password = model.Password,
                position = model.Position,
                isactive_status = model.IsActiveStatus
            };
        }

        private void SaveUserToDatabase(Models.User user)
        {
            using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString))
            {
                connection.Open();
                string query = "INSERT INTO Users (user_id, name, email, password, position, isactive_status) VALUES (@UserId, @Name, @Email, @Password, @Position, @IsActiveStatus)";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserId", user.user_id);
                command.Parameters.AddWithValue("@Name", user.name);
                command.Parameters.AddWithValue("@Email", user.email);
                command.Parameters.AddWithValue("@Password", user.password);
                command.Parameters.AddWithValue("@Position", user.position);
                command.Parameters.AddWithValue("@IsActiveStatus", user.isactive_status);
                command.ExecuteNonQuery();
            }
        }

        private List<SelectListItem> GetPositionList1()
        {
            return new List<SelectListItem>
    {
        new SelectListItem { Value = "Trainee", Text = "Trainee" },
        new SelectListItem { Value = "Senior Software Engineer", Text = "Senior Software Engineer" },
        new SelectListItem { Value = "Intern", Text = "Intern" },
        new SelectListItem { Value = "HR", Text = "HR" },
        new SelectListItem { Value = "Management", Text = "Management" }
        // Add more positions as needed
    };
        }



        [HttpPost]
        public JsonResult GetRoomName(int roomId)
        {
            string roomName = "";

            // Connect to the database and retrieve the room name based on the roomId
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT room_name FROM Rooms WHERE room_id = @RoomId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@RoomId", roomId);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                if (reader.Read())
                {
                    // Retrieve the room name from the reader
                    roomName = reader["room_name"].ToString();
                }
            }

            return Json(new { success = true, roomName });
        }

        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }
        [HttpPost]
        public ActionResult ForgotPassword(ForgotPasswordModel model)
        {
            if (!ModelState.IsValid)
            {
                // If model validation fails, return the view with validation errors
                return View(model);
            }

            try
            {
                // Generate a random password
                string newPassword = GenerateRandomPassword();

                // Send the new password to the user's email address
                bool emailSent = SendPasswordResetEmail(
                    model.Email,
                    newPassword,
                    "smtpout.secureserver.net", // Your SMTP server address
                    587, // The port number of your SMTP server
                    "deepak.patidar@averybit.in", // Your email address
                    "Deep1221@", // Your email password
                    true // Enable SSL/TLS
                );

                if (emailSent)
                {
                    // Update the user's password with the new one (you may have a separate logic for this)
                    UpdateUserPassword(model.Email, newPassword);

                    // Redirect to a page indicating that the reset email has been sent
                    return RedirectToAction("ResetEmailSent");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to send the password reset email. Please try again later.");
                }
            }
            catch (Exception ex)
            {
                // Handle email sending error
                ModelState.AddModelError("", "Failed to send the password reset email: " + ex.Message);
            }

            // If email sending fails, return the view with appropriate errors
            return View(model);
        }
        public ActionResult ResetEmailSent()
        {
            return View();
        }

        private void UpdateUserPassword(string email, string newPassword)
        {
            // Assuming you are using a database, you need to establish a connection and execute the update query.
            // Replace with your actual connection string
            string connectionString = ConfigurationManager.ConnectionStrings["MyConnectionString"]?.ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Open the connection
                connection.Open();

                // Prepare the update query
                string updateQuery = "UPDATE Users SET Password = @NewPassword WHERE Email = @Email";

                using (SqlCommand command = new SqlCommand(updateQuery, connection))
                {
                    // Set the parameters
                    command.Parameters.AddWithValue("@NewPassword", newPassword);
                    command.Parameters.AddWithValue("@Email", email);

                    // Execute the update query
                    command.ExecuteNonQuery();
                }

                // Close the connection
                connection.Close();
            }
        }


        private string GenerateRandomPassword()
        {
            const string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            Random random = new Random();
            StringBuilder password = new StringBuilder();

            for (int i = 0; i < 8; i++)
            {
                int index = random.Next(allowedChars.Length);
                password.Append(allowedChars[index]);
            }

            return password.ToString();
        }

        private bool SendPasswordResetEmail(string email, string password, string smtpServer, int smtpPort, string smtpUsername, string smtpPassword, bool enableSsl)
        {
            try
            {
                // Set up the email message
                MailMessage message = new MailMessage();
                message.From = new MailAddress("deepak.patidar@averybit.in"); // Your email address
                message.To.Add(new MailAddress(email));
                message.Subject = "Password Reset";
                message.Body = $"Your new password is: {password}";

                // Configure the SMTP client
                SmtpClient smtpClient = new SmtpClient("smtpout.secureserver.net"); // Your SMTP server address
                smtpClient.Port = smtpPort; // The port number of your SMTP server
                smtpClient.Credentials = new NetworkCredential("deepak.patidar@averybit.in", "Deep1221@"); // Your email credentials
                smtpClient.EnableSsl = enableSsl;

                // Send the email
                smtpClient.Send(message);

                return true;
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during email sending
                // You can log the error or perform any desired actions
                return false;
            }
        }
        public ActionResult adminpage()
        {
            List<Meeting> adminpage = GetMeetingsFromDatabase();
            List<Room> rooms = FetchRoomsData();

            // Make sure rooms is not null before assigning it to ViewBag
            if (rooms != null)
            {
                ViewBag.Rooms = rooms;
            }
            else
            {
                // Handle the case where rooms is null (e.g., database query error)
                // You can set ViewBag.Rooms to an empty list or handle the error as appropriate for your application
                ViewBag.Rooms = new List<Room>();
            }
            List<User> users = FetchUsersData();
            ViewBag.Rooms = rooms; 
            ViewBag.Users = users;
            var positionList = GetPositionList1();
            ViewBag.PositionList = positionList;
           
            // Pass the rooms data to the view using ViewBag
            return View(adminpage);
        }
        private List<Room> FetchRoomsData()
        {
            List<Room> rooms = new List<Room>();
            string connectionString = ConfigurationManager.ConnectionStrings["MyConnectionString"]?.ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT room_id, room_name, short_code,capacity FROM Rooms"; // Adjust column names accordingly
                SqlCommand command = new SqlCommand(query, connection);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Room room = new Room
                    {
                        RoomId = reader["room_id"] != DBNull.Value ? (int)reader["room_id"] : 0,
                        RoomName = reader["room_name"] != DBNull.Value ? (string)reader["room_name"] : string.Empty,
                       RoomCode = reader["short_code"] != DBNull.Value ? (string)reader["short_code"] : string.Empty,
                        Capacity = reader["capacity"] != DBNull.Value ? (int)reader["capacity"] : 0,
                    };

                    rooms.Add(room);
                }

                reader.Close();
            }

            return rooms;
        }

        private List<User> FetchUsersData()
        {
            List<User> users = new List<User>();
            string connectionString = ConfigurationManager.ConnectionStrings["MyConnectionString"]?.ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT user_id, name, email,position FROM Users"; // Adjust column names accordingly
                SqlCommand command = new SqlCommand(query, connection);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    User user = new User
                    {
                        UserId = reader["user_id"] != DBNull.Value ? (int)reader["user_id"] : 0,
                       UserName = reader["name"] != DBNull.Value ? (string)reader["name"] : string.Empty,
                       email = reader["email"] != DBNull.Value ? (string)reader["email"] : string.Empty,
                       position = reader["position"] != DBNull.Value ? (string)reader["position"] : string.Empty,
                    };

                    users.Add(user);
                }

                reader.Close();
            }

            return users;
        }
        [HttpPost]
        public ActionResult AddRoom(Room room)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Generate a unique room ID (incremented by one from the last room ID)
                    int lastRoomId;
                    using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString))
                    {
                        connection.Open();
                        string query = "SELECT ISNULL(MAX(room_id), 0) FROM Rooms";
                        SqlCommand command = new SqlCommand(query, connection);
                        lastRoomId = (int)command.ExecuteScalar();
                    }
                    int newRoomId = lastRoomId + 1;

                    // Save the room to the database
                    using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MyConnectionString"].ConnectionString))
                    {
                        connection.Open();
                        string query = "INSERT INTO Rooms (room_id, room_name, short_code, capacity) VALUES (@RoomId, @RoomName, @RoomCode, @RoomCapacity)";
                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@RoomId", newRoomId);
                        command.Parameters.AddWithValue("@RoomName", room.RoomName);
                        command.Parameters.AddWithValue("@RoomCode", room.RoomCode);
                        command.Parameters.AddWithValue("@RoomCapacity", room.Capacity);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    // Handle database error
                    string errorMessage = "Room addition failed. Please try again. Error: " + ex.Message;
                    TempData["ErrorMessage"] = errorMessage;
                }
            }

            // If the model state is invalid, return the error message
            TempData["ErrorMessage"] = "Invalid room data";

            return RedirectToAction("AdminPage", "Home");
        }



        [HttpPost]
        public ActionResult Edit(Meeting meeting)
        {
            try
            {
                int userId = GetLoggedInUserId();
                bool isAuthorized = IsMeetingCreatedByUser(meeting.MeetingId, userId);

                // Check if the user is an admin
                bool isAdmin = IsUserAdmin(userId);

                // Allow admin to edit all meetings
                if (!isAuthorized && !isAdmin)
                {
                    TempData["ErrorMessage"] = "You are not authorized to edit this meeting.";
                    return RedirectToAction("adminpage");
                }

                Room selectedRoom = GetRoomById(meeting.RoomId);
                if (selectedRoom == null)
                {
                    ViewBag.ErrorMessage = "Selected room not found.";
                    return RedirectToAction("adminpage");
                }

                meeting.RoomName = selectedRoom.RoomName;
                meeting.Capacity = selectedRoom.Capacity;

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    string query = "UPDATE Meetings SET meeting_title = @Title, start_time = @StartTime, end_time = @EndTime, room_id = @RoomId WHERE meeting_id = @MeetingId";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Title", meeting.MeetingTitle);
                    command.Parameters.AddWithValue("@StartTime", meeting.StartTime);
                    command.Parameters.AddWithValue("@EndTime", meeting.EndTime);
                    command.Parameters.AddWithValue("@RoomId", meeting.RoomId);
                    command.Parameters.AddWithValue("@MeetingId", meeting.MeetingId);

                    connection.Open();
                    command.ExecuteNonQuery();
                }

                ViewBag.AlertMessage = "Meeting updated successfully.";
            }
            catch (Exception ex)
            {
                // Handle the exception, log error, or display an error message
                ViewBag.ErrorMessage = ex.Message;
            }

            return RedirectToAction("adminpage");
        }

        private bool IsUserAdmin(int userId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT COUNT(*) FROM Users WHERE user_id = @UserId AND isactive_status=1";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserId", userId);

                connection.Open();
                int count = (int)command.ExecuteScalar();

                return count > 0;
            }
        }
        // GET: Edit Room
        [HttpPost]
        public ActionResult EditRoom(int roomId, string editRoomName, string editRoomCode, int editCapacity)
        {
            // Update the room details using the provided information
            UpdateRoom(roomId, editRoomName, editRoomCode, editCapacity);

            // Redirect to the rooms page or return JSON response as needed
            return RedirectToAction("adminpage");
        }

        private void UpdateRoom(int roomId, string RoomName, string editRoomCode, int Capacity)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "UPDATE Rooms SET room_name = @RoomName,short_code=@editRoomCode, capacity = @Capacity WHERE room_id = @RoomId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@RoomName", RoomName);
                command.Parameters.AddWithValue("@editRoomCode", editRoomCode);
                command.Parameters.AddWithValue("@Capacity", Capacity);
                command.Parameters.AddWithValue("@RoomId", roomId);

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        [HttpPost]
        public ActionResult DeleteRoom(int roomId)
        {
            // Implement your logic to delete the room with the given roomId
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "DELETE FROM Rooms WHERE room_id = @RoomId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@RoomId", roomId);

                connection.Open();
                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    // Room deleted successfully
                    TempData["Message"] = "Room deleted successfully.";
                }
                else
                {
                    // Failed to delete the room
                    TempData["Message"] = "Failed to delete the room.";
                }
            }

            // Redirect to the updated rooms page or perform any other desired action
            return RedirectToAction("adminpage");
        }
        [HttpPost]
        public ActionResult Delete(int meetingId)
        {
            // Implement your logic to delete the meeting with the given meetingId
            // This could involve deleting the meeting from the database or any other data storage

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "DELETE FROM Meetings WHERE meeting_id = @MeetingId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@MeetingId", meetingId);

                connection.Open();
                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    // Meeting deleted successfully
                    TempData["Message"] = "Meeting deleted successfully.";
                }
                else
                {
                    // Failed to delete the meeting
                    TempData["Message"] = "Failed to delete the meeting.";
                }
            }

            // Redirect to the updated meetings page or perform any other desired action
            return RedirectToAction("adminpage");
        }

        [HttpPost]
        public ActionResult EditUser(int userId, string userName, string email, string position)
        {
            // Update the user in the database
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "UPDATE Users SET name = @UserName, email = @Email, position = @Position WHERE user_id = @UserId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@Email", email);
                command.Parameters.AddWithValue("@Position", position);
                command.Parameters.AddWithValue("@UserId", userId);

                connection.Open();
                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    // User updated successfully in the database
                    TempData["Message"] = "User updated successfully.";
                }
                else
                {
                    // Failed to update the user in the database
                    TempData["Message"] = "Failed to update the user.";
                }
            }

            // Assuming you have a collection of users called "Users" in ViewBag, you can update the user in the collection
            var users = ViewBag.Users as List<User>;
            if (users != null)
            {
                var userToUpdate = users.FirstOrDefault(u => u.UserId == userId);
                if (userToUpdate != null)
                {
                    userToUpdate.UserName = userName;
                    userToUpdate.Email = email;
                    userToUpdate.Position = position;
                }
            }

            // Redirect to the appropriate page after successful update
            return RedirectToAction("adminpage");
        }
        [HttpPost]
        public ActionResult DeleteUser(int userId)
        {
            // Delete the user from the database
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "DELETE FROM Users WHERE user_id = @UserId";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserId", userId);

                connection.Open();
                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    // User deleted successfully from the database
                    TempData["Message"] = "User deleted successfully.";
                }
                else
                {
                    // Failed to delete the user from the database
                    TempData["Message"] = "Failed to delete the user.";
                }
            }

            // Assuming you have a collection of users called "Users" in ViewBag, you can remove the user from the collection
            var users = ViewBag.Users as List<User>;
            if (users != null)
            {
                var userToDelete = users.FirstOrDefault(u => u.UserId == userId);
                if (userToDelete != null)
                {
                    users.Remove(userToDelete);
                }
            }

            // Redirect to the appropriate page after successful deletion
            return RedirectToAction("adminpage");
        }


    }
}
