using Ticketing.Application.DTOs;

namespace Ticketing.Application.Services;

public interface ISystemSettingsService
{
    Task<SystemSettingsResponse> GetSystemSettingsAsync();
    Task<SystemSettingsResponse> UpdateSystemSettingsAsync(SystemSettingsUpdateRequest request);
}