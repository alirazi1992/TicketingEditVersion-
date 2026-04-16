using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Services;

public interface IUserPreferencesService
{
    Task<UserPreferencesResponse> GetPreferencesAsync(Guid userId);
    Task<UserPreferencesResponse> UpdatePreferencesAsync(Guid userId, UserPreferencesUpdateRequest request);
    Task<NotificationPreferencesResponse> GetNotificationPreferencesAsync(Guid userId);
    Task<NotificationPreferencesResponse> UpdateNotificationPreferencesAsync(Guid userId, NotificationPreferencesUpdateRequest request);
}

public class UserPreferencesService : IUserPreferencesService
{
    private readonly IUserPreferencesRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UserPreferencesService(
        IUserPreferencesRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserPreferencesResponse> GetPreferencesAsync(Guid userId)
    {
        var preferences = await _repository.GetByUserIdAsync(userId);

        if (preferences == null)
        {
            // Return defaults if no preferences exist
            return new UserPreferencesResponse
            {
                Theme = "dark",
                FontSize = "md",
                Language = "fa",
                Direction = "rtl",
                Timezone = "Asia/Tehran",
                Notifications = new NotificationPreferencesResponse
                {
                    EmailEnabled = true,
                    PushEnabled = true,
                    SmsEnabled = false,
                    DesktopEnabled = true
                }
            };
        }

        // Ensure notification preferences have defaults if they were added later
        // This handles migration of existing records
        if (!preferences.EmailEnabled && !preferences.PushEnabled && 
            !preferences.SmsEnabled && !preferences.DesktopEnabled)
        {
            // This might be an old record without notification prefs - set defaults
            preferences.EmailEnabled = true;
            preferences.PushEnabled = true;
            preferences.SmsEnabled = false;
            preferences.DesktopEnabled = true;
            await _repository.UpdateAsync(preferences);
            await _unitOfWork.SaveChangesAsync();
        }

        // System is Farsi-only: always return fa and rtl
        return new UserPreferencesResponse
        {
            Theme = preferences.Theme,
            FontSize = preferences.FontSize,
            Language = "fa",
            Direction = "rtl",
            Timezone = preferences.Timezone ?? "Asia/Tehran",
            Notifications = new NotificationPreferencesResponse
            {
                EmailEnabled = preferences.EmailEnabled,
                PushEnabled = preferences.PushEnabled,
                SmsEnabled = preferences.SmsEnabled,
                DesktopEnabled = preferences.DesktopEnabled
            }
        };
    }

    public async Task<UserPreferencesResponse> UpdatePreferencesAsync(Guid userId, UserPreferencesUpdateRequest request)
    {
        var preferences = await _repository.GetByUserIdAsync(userId);

        if (preferences == null)
        {
            // Create new preferences with notification defaults (system is Farsi-only)
            preferences = new UserPreferences
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Theme = request.Theme,
                FontSize = request.FontSize,
                Language = "fa",
                Timezone = request.Timezone,
                EmailEnabled = true,
                PushEnabled = true,
                SmsEnabled = false,
                DesktopEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _repository.AddAsync(preferences);
        }
        else
        {
            // Update existing preferences (system is Farsi-only: always store fa)
            preferences.Theme = request.Theme;
            preferences.FontSize = request.FontSize;
            preferences.Language = "fa";
            preferences.Timezone = request.Timezone;
            preferences.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(preferences);
        }

        await _unitOfWork.SaveChangesAsync();

        // System is Farsi-only: always return fa and rtl
        return new UserPreferencesResponse
        {
            Theme = preferences.Theme,
            FontSize = preferences.FontSize,
            Language = "fa",
            Direction = "rtl",
            Timezone = preferences.Timezone ?? "Asia/Tehran",
            Notifications = new NotificationPreferencesResponse
            {
                EmailEnabled = preferences.EmailEnabled,
                PushEnabled = preferences.PushEnabled,
                SmsEnabled = preferences.SmsEnabled,
                DesktopEnabled = preferences.DesktopEnabled
            }
        };
    }

    public async Task<NotificationPreferencesResponse> GetNotificationPreferencesAsync(Guid userId)
    {
        var preferences = await _repository.GetByUserIdAsync(userId);

        if (preferences == null)
        {
            // Return defaults if no preferences exist
            return new NotificationPreferencesResponse
            {
                EmailEnabled = true,
                PushEnabled = true,
                SmsEnabled = false,
                DesktopEnabled = true
            };
        }

        return new NotificationPreferencesResponse
        {
            EmailEnabled = preferences.EmailEnabled,
            PushEnabled = preferences.PushEnabled,
            SmsEnabled = preferences.SmsEnabled,
            DesktopEnabled = preferences.DesktopEnabled
        };
    }

    public async Task<NotificationPreferencesResponse> UpdateNotificationPreferencesAsync(Guid userId, NotificationPreferencesUpdateRequest request)
    {
        var preferences = await _repository.GetByUserIdAsync(userId);

        if (preferences == null)
        {
            // Create new preferences with defaults
            preferences = new UserPreferences
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Theme = "system",
                FontSize = "md",
                Language = "fa",
                Timezone = "Asia/Tehran",
                EmailEnabled = request.EmailEnabled,
                PushEnabled = request.PushEnabled,
                SmsEnabled = request.SmsEnabled,
                DesktopEnabled = request.DesktopEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _repository.AddAsync(preferences);
        }
        else
        {
            // Update notification preferences
            preferences.EmailEnabled = request.EmailEnabled;
            preferences.PushEnabled = request.PushEnabled;
            preferences.SmsEnabled = request.SmsEnabled;
            preferences.DesktopEnabled = request.DesktopEnabled;
            preferences.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(preferences);
        }

        await _unitOfWork.SaveChangesAsync();

        return new NotificationPreferencesResponse
        {
            EmailEnabled = preferences.EmailEnabled,
            PushEnabled = preferences.PushEnabled,
            SmsEnabled = preferences.SmsEnabled,
            DesktopEnabled = preferences.DesktopEnabled
        };
    }
}

