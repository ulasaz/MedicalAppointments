using Finbuckle.MultiTenant.Abstractions;

namespace Orders.Models;
public enum OrderStatus
{
     Pending,
     Confirmed,
     Rejected,
     Ready,
     Completed ,
     Cancelled
}