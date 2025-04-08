using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBot
{
    public class Plan
    {
        public int Id { get; set; }
        public string TextPlan { get; set; } = string.Empty;
        public DateTime DateTimePlan { get; set; }
    }
}
