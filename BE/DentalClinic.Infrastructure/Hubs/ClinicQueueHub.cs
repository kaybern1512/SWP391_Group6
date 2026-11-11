using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace DentalClinic.Infrastructure.Hubs;

public interface IClinicQueueClient
{
    Task ReceiveAppointmentCreated(object data);
    Task ReceivePatientCheckedIn(object data);
    Task ReceivePatientInService(object data);
    Task ReceiveTreatmentCompleted(object data);
    Task ReceiveQueueUpdated(object data);
}

public class ClinicQueueHub : Hub<IClinicQueueClient>
{
    public async Task JoinDepartmentQueue(long departmentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dept_{departmentId}");
    }

    public async Task LeaveDepartmentQueue(long departmentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dept_{departmentId}");
    }

    public async Task JoinClinicQueue()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "clinic_reception_queue");
    }

    public async Task LeaveClinicQueue()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "clinic_reception_queue");
    }
}
