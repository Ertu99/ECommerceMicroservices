using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Application.DTOs.Events
{
    public class IntegrationEventEnvelope
    {
        public string EventType { get; set; } = "";
        public string Payload { get; set; } = "";
    }
}
