using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Catalyst;

namespace TextUtils.Models
{
    public class RecommendationDocument
    {
        public string Body { get; set; }
        public string AnonymousBody { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public Document Document { get; set; }
    }
}
