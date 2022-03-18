using Microsoft.AspNet.Identity;
using Projekt_Przychodnia_TsWM.Models;
using System;
using System.Drawing;
using System.Linq;
using System.Web.Mvc;

namespace Projekt_Przychodnia_TsWM.Controllers
{
    public class CalendarController : Controller
    {
        // GET: Calendar
        public ActionResult Index()
        {
            return View();
        }

        [Authorize]
        public JsonResult GetEvents()
        {
            var userId = User.Identity.GetUserId();

            using (ApplicationDbContext dbContext = new ApplicationDbContext())
            {
                bool isAdmin = User.IsInRole("Admin");
                Func<Event, bool> p;
                if (isAdmin)
                {
                    p = x => true;
                }
                else
                {
                    p = x => x.UserId == userId;
                }
                var events = dbContext.Events.Where(p).ToList();

                if (isAdmin)
                {
                    var userIds = events.Select(x => x.UserId);
                    var users = dbContext.Users.Where(x => userIds.Any(y => y == x.Id)).ToDictionary(x => x.Id, x => x.FirstName + " " + x.LastName);

                    foreach (Event e in events)
                    {
                        e.Subject = e.Subject + " - " + users[e.UserId];
                        e.ThemeColor = GetThemeColor(e.UserId);
                    }
                }
                else
                {
                    foreach (Event e in events)
                    {
                        e.ThemeColor = GetThemeColor(userId);
                    }
                }

                return new JsonResult { Data = events, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
        }

        [Authorize]
        [HttpPost]
        public JsonResult DeleteEvent(int eventID)
        {
            var userId = User.Identity.GetUserId();
            var status = false;

            using (ApplicationDbContext dbContext = new ApplicationDbContext())
            {
                bool isAdmin = User.IsInRole("Admin");
                Func<Event, bool> p;
                if (isAdmin)
                {
                    p = x => x.EventID == eventID;
                }
                else
                {
                    p = x => x.EventID == eventID && x.UserId == userId;
                }
                var v = dbContext.Events.Where(p).FirstOrDefault();

                if (v != null)
                {
                    dbContext.Events.Remove(v);
                    dbContext.SaveChanges();
                    status = true;
                }
            }
            return new JsonResult { Data = new { status = status, message = "" } };
        }

        [Authorize]
        [HttpPost]
        public JsonResult SaveOrUpdateEvent(Event e)
        {
            var userId = User.Identity.GetUserId();
            var status = false;
            (bool isAvailable, string message) check = (false, "");

            using (ApplicationDbContext dbContext = new ApplicationDbContext())
            {
                bool isAdmin = User.IsInRole("Admin");
                //Aktualizacja
                if (e.EventID >= 1)
                {
                    Func<Event, bool> p;
                    if (isAdmin)
                    {
                        p = x => x.EventID == e.EventID;
                    }
                    else
                    {
                        p = x => x.EventID == e.EventID && x.UserId == userId;
                    }
                    var v = dbContext.Events.Where(p).FirstOrDefault();

                    if (v != null)
                    {
                        v.Subject = e.Subject.Contains('-') ? e.Subject.Substring(0, e.Subject.IndexOf('-') - 1) : e.Subject;
                        v.Start = e.Start;
                        v.End = e.Start.AddMinutes(30);
                        v.Description = e.Description;

                        check = IsDateAvailable(userId, v.Start, v.End ?? v.Start);
                    }
                }
                //Tworzenie
                else
                {
                    if (!isAdmin)
                    {
                        e.UserId = userId;
                        e.End = e.Start.AddMinutes(30);
                        e.ThemeColor = GetThemeColor(userId);

                        check = IsDateAvailable(userId, e.Start, e.End ?? e.Start);
                        if (check.isAvailable)
                        {
                            dbContext.Events.Add(e);
                        }
                    }
                }

                if (check.isAvailable)
                {
                    dbContext.SaveChanges();
                }

                status = true;
            }
            return new JsonResult { Data = new { status = status, message = check.message } };
        }

        //sprawdzić czy data nie jest w przeszłości + sprawdzić czy daty nie nakładają się na siebie 
        private static (bool, string) IsDateAvailable(string userId, DateTime start, DateTime end)
        {
            (bool isAvailable, string message) check = (true, "");

            if (start < DateTime.Now)
            {
                check = (false, "Data wizyty nie może być z przeszłości");
            }

            if (check.isAvailable)
            {
                using (ApplicationDbContext dbContext = new ApplicationDbContext())
                {
                    var events = dbContext.Events.Where(x => x.UserId == userId).ToList();

                    foreach (Event e in events)
                    {
                        if (start.Date == e.Start.Date && e.End.HasValue && start.TimeOfDay <= e.End.Value.TimeOfDay)
                        {
                            check = (false, "Data nowej wizyty nie może nachodzić na inną już zaplanowaną wizytę");
                            break;
                        }
                    }
                }
            }

            //bool isAvailable = false;

            //using (ApplicationDbContext dbContext = new ApplicationDbContext())
            //{
            //    var v = dbContext.Events.Select(x => x).FirstOrDefault();
            //    var selectedEvent = dbContext.Events.GroupBy(x => x).Where(g => g.Count() > 1).Select(y => y.Key);

            //    DateTime dateTimeNow = new DateTime();

            //    if (v.Start.Date < dateTimeNow)
            //    {
            //        return isAvailable;
            //    }
            //    else
            //    {
            //        isAvailable = true;
            //        return isAvailable;
            //    }
            //    //sprawdzenie czy daty sie na siebie nie nakładają
            //}

            return check;
        }

        private string GetThemeColor(string userGuid)
        {
            return Color.FromKnownColor(((KnownColor[])Enum.GetValues(typeof(KnownColor)))[(Math.Abs(new Guid(userGuid).GetHashCode()) % 141) + 26]).Name;
        }
    }
}