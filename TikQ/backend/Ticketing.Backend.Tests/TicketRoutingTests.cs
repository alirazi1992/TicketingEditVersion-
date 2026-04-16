using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Data;
using Ticketing.Backend.Infrastructure.Data.Repositories;
using Xunit;

namespace Ticketing.Backend.Tests;

public class TicketRoutingTests
{
    private static (AppDbContext context, ITicketService service) CreateService()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        // Repositories
        var ticketRepository = new TicketRepository(context);
        var ticketMessageRepository = new TicketMessageRepository(context);
        var technicianRepository = new TechnicianRepository(context);
        var userRepository = new UserRepository(context);
        var categoryRepository = new CategoryRepository(context);
        var assignmentRepository = new TicketTechnicianAssignmentRepository(context);
        var activityEventRepository = new TicketActivityEventRepository(context);
        var ticketUserStateRepository = new TicketUserStateRepository(context);
        var unitOfWork = new UnitOfWork(context);

        // Stub services
        var technicianService = new StubTechnicianService();
        var systemSettingsService = new StubSystemSettingsService();
        var smartAssignmentService = new StubSmartAssignmentService();

        var service = new TicketService(
            ticketRepository,
            ticketMessageRepository,
            technicianRepository,
            userRepository,
            categoryRepository,
            unitOfWork,
            technicianService,
            systemSettingsService,
            smartAssignmentService,
            assignmentRepository,
            activityEventRepository,
            ticketUserStateRepository);

        return (context, service);
    }

    [Fact]
    public async Task CreateTicket_Assigns_Technicians_By_Subcategory()
    {
        var (context, service) = CreateService();

        var category = new Category { Id = 1, Name = "Hardware", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "Printer", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);

        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", Role = UserRole.Client };
        var techUser = new User { Id = Guid.NewGuid(), Email = "tech@test.com", FullName = "Tech", Role = UserRole.Technician };
        context.Users.AddRange(clientUser, techUser);

        var technician = new Technician { Id = Guid.NewGuid(), FullName = "Tech", Email = "tech@test.com", IsActive = true, UserId = techUser.Id };
        context.Technicians.Add(technician);
        context.TechnicianSubcategoryPermissions.Add(new TechnicianSubcategoryPermission
        {
            Id = Guid.NewGuid(),
            TechnicianId = technician.Id,
            SubcategoryId = subcategory.Id
        });

        await context.SaveChangesAsync();

        var request = new TicketCreateRequest
        {
            Title = "Printer not working",
            Description = "Paper jam",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.Medium
        };

        var created = await service.CreateTicketAsync(clientUser.Id, request);

        Assert.NotNull(created);
        Assert.NotNull(created!.AssignedTechnicians);
        Assert.Single(created.AssignedTechnicians!);
        Assert.Equal(techUser.Id, created.AssignedTechnicians![0].TechnicianUserId);
    }

    [Fact]
    public async Task CreateTicket_NoMatch_Remains_Unassigned()
    {
        var (context, service) = CreateService();

        var category = new Category { Id = 2, Name = "Software", IsActive = true };
        var subcategory = new Subcategory { Id = 20, CategoryId = 2, Name = "OS", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);

        var clientUser = new User { Id = Guid.NewGuid(), Email = "client2@test.com", FullName = "Client2", Role = UserRole.Client };
        context.Users.Add(clientUser);
        await context.SaveChangesAsync();

        var request = new TicketCreateRequest
        {
            Title = "OS crash",
            Description = "Blue screen",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.High
        };

        var created = await service.CreateTicketAsync(clientUser.Id, request);

        Assert.NotNull(created);
        Assert.True(created!.AssignedTechnicians == null || created.AssignedTechnicians.Count == 0);
        Assert.Null(created.AssignedToUserId);
    }

    [Fact]
    public async Task TicketSeenState_Tracks_LatestActivity()
    {
        var (context, service) = CreateService();

        var user = new User { Id = Guid.NewGuid(), Email = "client3@test.com", FullName = "Client3", Role = UserRole.Client };
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Network issue",
            Description = "No internet",
            CategoryId = 1,
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Submitted,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        context.Users.Add(user);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        var firstList = await service.GetTicketsAsync(user.Id, UserRole.Client, null, null, null, null, null);
        Assert.Single(firstList);
        Assert.True(firstList.First().IsUnseen);

        var marked = await service.MarkTicketSeenAsync(ticket.Id, user.Id, UserRole.Client);
        Assert.True(marked);

        var secondList = await service.GetTicketsAsync(user.Id, UserRole.Client, null, null, null, null, null);
        Assert.Single(secondList);
        Assert.False(secondList.First().IsUnseen);

        ticket.UpdatedAt = DateTime.UtcNow.AddMinutes(1);
        context.Tickets.Update(ticket);
        await context.SaveChangesAsync();

        var thirdList = await service.GetTicketsAsync(user.Id, UserRole.Client, null, null, null, null, null);
        Assert.Single(thirdList);
        Assert.True(thirdList.First().IsUnseen);
    }

    private sealed class StubTechnicianService : ITechnicianService
    {
        public Task<IEnumerable<TechnicianResponse>> GetAllTechniciansAsync() => Task.FromResult<IEnumerable<TechnicianResponse>>(Array.Empty<TechnicianResponse>());
        public Task<TechnicianResponse?> GetTechnicianByIdAsync(Guid id) => Task.FromResult<TechnicianResponse?>(null);
        public Task<TechnicianResponse?> GetTechnicianByUserIdAsync(Guid userId) => Task.FromResult<TechnicianResponse?>(null);
        public Task<TechnicianResponse> CreateTechnicianAsync(TechnicianCreateRequest request) => throw new NotImplementedException();
        public Task<TechnicianResponse?> UpdateTechnicianAsync(Guid id, TechnicianUpdateRequest request) => throw new NotImplementedException();
        public Task<TechnicianResponse?> UpdateTechnicianExpertiseAsync(Guid id, List<int> subcategoryIds) => Task.FromResult<TechnicianResponse?>(null);
        public Task<bool> UpdateTechnicianStatusAsync(Guid id, bool isActive) => Task.FromResult(false);
        public Task<bool> IsTechnicianActiveAsync(Guid id) => Task.FromResult(false);
        public Task<(LinkUserResult result, TechnicianResponse? technician)> LinkUserAsync(Guid technicianId, Guid userId) => Task.FromResult((LinkUserResult.TechnicianNotFound, (TechnicianResponse?)null));
        public Task<IEnumerable<TechnicianResponse>> GetAssignableTechniciansAsync() => Task.FromResult<IEnumerable<TechnicianResponse>>(Array.Empty<TechnicianResponse>());
    }

    private sealed class StubSystemSettingsService : ISystemSettingsService
    {
        public Task<SystemSettingsResponse> GetSystemSettingsAsync() => Task.FromResult(new SystemSettingsResponse());
        public Task<SystemSettingsResponse> UpdateSystemSettingsAsync(SystemSettingsUpdateRequest request) => Task.FromResult(new SystemSettingsResponse());
    }

    private sealed class StubSmartAssignmentService : ISmartAssignmentService
    {
        public Task<Guid?> AssignTechnicianToTicketAsync(Guid ticketId) => Task.FromResult<Guid?>(null);
        public Task<int> AssignUnassignedTicketsAsync(DateTime? startDate = null, DateTime? endDate = null) => Task.FromResult(0);
    }
}

