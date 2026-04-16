using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// Tests for ticket status synchronization across all dashboards.
/// 
/// REQUIREMENT: Ticket status must be synchronized across the entire system.
/// Any status update from ANY dashboard (Client / Technician / Supervisor / Admin)
/// must be persisted once and then every involved user must see the SAME latest status.
/// </summary>
public class StatusSynchronizationTests
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
        var supervisorLinkRepository = new SupervisorTechnicianLinkRepository(context);
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
            ticketUserStateRepository,
            supervisorLinkRepository);

        return (context, service);
    }

    /// <summary>
    /// Test scenario from requirements:
    /// 1) Create ticket
    /// 2) Assign Technician1 + Technician2
    /// 3) Technician2 changes status to Solved
    /// 4) Verify all actors see status Solved:
    ///    - Technician1 => sees status Solved
    ///    - Technician2 => sees status Solved
    ///    - Admin => sees status Solved
    ///    - Client => sees status Solved (or mapped if applicable)
    /// 5) Verify Ticket.Status updated once + TicketActivity appended + LastActivityAt updated
    /// </summary>
    [Fact]
    public async Task StatusChange_IsSynchronized_AcrossAllDashboards()
    {
        var (context, service) = CreateService();

        // Setup: Create users
        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client User", Role = UserRole.Client };
        var techUser1 = new User { Id = Guid.NewGuid(), Email = "tech1@test.com", FullName = "Technician 1", Role = UserRole.Technician };
        var techUser2 = new User { Id = Guid.NewGuid(), Email = "tech2@test.com", FullName = "Technician 2", Role = UserRole.Technician };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin User", Role = UserRole.Admin };
        
        context.Users.AddRange(clientUser, techUser1, techUser2, adminUser);

        // Setup: Create technicians
        var technician1 = new Technician { Id = Guid.NewGuid(), FullName = "Technician 1", Email = "tech1@test.com", IsActive = true, UserId = techUser1.Id };
        var technician2 = new Technician { Id = Guid.NewGuid(), FullName = "Technician 2", Email = "tech2@test.com", IsActive = true, UserId = techUser2.Id };
        context.Technicians.AddRange(technician1, technician2);

        // Setup: Create category and subcategory
        var category = new Category { Id = 1, Name = "Network", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "Connectivity", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);

        await context.SaveChangesAsync();

        // Step 1: Create ticket
        var request = new TicketCreateRequest
        {
            Title = "Network connectivity issue",
            Description = "Cannot connect to VPN",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.High
        };

        var createdTicket = await service.CreateTicketAsync(clientUser.Id, request);
        Assert.NotNull(createdTicket);
        var ticketId = createdTicket!.Id;
        Assert.Equal(TicketStatus.Submitted, createdTicket.CanonicalStatus);
        Assert.Equal(TicketStatus.Submitted, createdTicket.DisplayStatus);

        // Step 2: Assign both technicians (simulating admin assignment)
        var assignResult = await service.AssignTechniciansAsync(
            ticketId,
            new List<Guid> { techUser1.Id, techUser2.Id },
            adminUser.Id);
        
        Assert.NotNull(assignResult);
        Assert.Equal(TicketStatus.Open, assignResult!.CanonicalStatus); // Status should be Open after assignment

        // Step 3: Technician2 changes status to Solved
        var statusChangeResult = await service.ChangeStatusAsync(
            ticketId,
            TicketStatus.Solved,
            techUser2.Id,
            UserRole.Technician);

        Assert.True(statusChangeResult.Success);
        Assert.Equal(TicketStatus.Open, statusChangeResult.OldStatus);
        Assert.Equal(TicketStatus.Solved, statusChangeResult.NewStatus);

        // Step 4: Verify all actors see the SAME status (Solved)
        
        // 4a. Technician1 sees Solved
        var ticketForTech1 = await service.GetTicketAsync(ticketId, techUser1.Id, UserRole.Technician);
        Assert.NotNull(ticketForTech1);
        Assert.Equal(TicketStatus.Solved, ticketForTech1!.CanonicalStatus);

        // 4b. Technician2 sees Solved
        var ticketForTech2 = await service.GetTicketAsync(ticketId, techUser2.Id, UserRole.Technician);
        Assert.NotNull(ticketForTech2);
        Assert.Equal(TicketStatus.Solved, ticketForTech2!.CanonicalStatus);

        // 4c. Admin sees Solved
        var ticketForAdmin = await service.GetTicketAsync(ticketId, adminUser.Id, UserRole.Admin);
        Assert.NotNull(ticketForAdmin);
        Assert.Equal(TicketStatus.Solved, ticketForAdmin!.CanonicalStatus);

        // 4d. Client sees Solved
        var ticketForClient = await service.GetTicketAsync(ticketId, clientUser.Id, UserRole.Client);
        Assert.NotNull(ticketForClient);
        Assert.Equal(TicketStatus.Solved, ticketForClient!.CanonicalStatus);

        // Step 5: Verify the database has correct state
        var ticketInDb = await context.Tickets.FindAsync(ticketId);
        Assert.NotNull(ticketInDb);
        Assert.Equal(TicketStatus.Solved, ticketInDb!.Status);
        Assert.NotNull(ticketInDb.UpdatedAt);

        // 5b. Verify TicketActivity was logged
        var activityEvents = await context.TicketActivityEvents
            .Where(e => e.TicketId == ticketId && e.EventType == "StatusChanged")
            .ToListAsync();
        
        Assert.True(activityEvents.Count >= 1, "At least one StatusChanged event should be logged");
        var statusChangeEvent = activityEvents.Last();
        Assert.Equal("Open", statusChangeEvent.OldStatus);
        Assert.Equal("Solved", statusChangeEvent.NewStatus);
        Assert.Equal(techUser2.Id, statusChangeEvent.ActorUserId);
    }

    /// <summary>
    /// Test that ticket list endpoints also return the updated status for multi-assigned technicians
    /// </summary>
    [Fact]
    public async Task ListEndpoints_ReturnUpdatedStatus_ForAllTechnicians()
    {
        var (context, service) = CreateService();

        // Setup
        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", Role = UserRole.Client };
        var techUser1 = new User { Id = Guid.NewGuid(), Email = "tech1@test.com", FullName = "Tech1", Role = UserRole.Technician };
        var techUser2 = new User { Id = Guid.NewGuid(), Email = "tech2@test.com", FullName = "Tech2", Role = UserRole.Technician };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin", Role = UserRole.Admin };
        
        context.Users.AddRange(clientUser, techUser1, techUser2, adminUser);

        var technician1 = new Technician { Id = Guid.NewGuid(), FullName = "Tech1", Email = "tech1@test.com", IsActive = true, UserId = techUser1.Id };
        var technician2 = new Technician { Id = Guid.NewGuid(), FullName = "Tech2", Email = "tech2@test.com", IsActive = true, UserId = techUser2.Id };
        context.Technicians.AddRange(technician1, technician2);

        var category = new Category { Id = 1, Name = "Software", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "Bug", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);

        await context.SaveChangesAsync();

        // Create and assign ticket
        var createRequest = new TicketCreateRequest
        {
            Title = "Bug report",
            Description = "App crashes",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.High
        };

        var ticket = await service.CreateTicketAsync(clientUser.Id, createRequest);
        Assert.NotNull(ticket);
        
        await service.AssignTechniciansAsync(ticket!.Id, new List<Guid> { techUser1.Id, techUser2.Id }, adminUser.Id);

        // Change status to InProgress
        await service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, techUser1.Id, UserRole.Technician);

        // Verify list endpoint for Tech1
        var tech1List = await service.GetTicketsAsync(techUser1.Id, UserRole.Technician, null, null, null, null, null);
        var tech1Ticket = tech1List.FirstOrDefault(t => t.Id == ticket.Id);
        Assert.NotNull(tech1Ticket);
        Assert.Equal(TicketStatus.InProgress, tech1Ticket!.DisplayStatus);

        // Verify list endpoint for Tech2
        var tech2List = await service.GetTicketsAsync(techUser2.Id, UserRole.Technician, null, null, null, null, null);
        var tech2Ticket = tech2List.FirstOrDefault(t => t.Id == ticket.Id);
        Assert.NotNull(tech2Ticket);
        Assert.Equal(TicketStatus.InProgress, tech2Ticket!.DisplayStatus);

        // Verify list endpoint for Client
        var clientList = await service.GetTicketsAsync(clientUser.Id, UserRole.Client, null, null, null, null, null);
        var clientTicket = clientList.FirstOrDefault(t => t.Id == ticket.Id);
        Assert.NotNull(clientTicket);
        Assert.Equal(TicketStatus.InProgress, clientTicket!.DisplayStatus);
    }

    /// <summary>
    /// Test that Redo status uses role-based display mapping:
    /// - canonicalStatus is always Redo in database
    /// - displayStatus is InProgress for Client, Redo for Technician/Admin
    /// 
    /// PHASE 5 TEST: Role-based status mapping
    /// </summary>
    [Fact]
    public async Task RedoStatus_DisplaysAsInProgress_ForClient()
    {
        var (context, service) = CreateService();

        // Setup
        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", Role = UserRole.Client };
        var techUser = new User { Id = Guid.NewGuid(), Email = "tech@test.com", FullName = "Tech", Role = UserRole.Technician };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin", Role = UserRole.Admin };
        
        context.Users.AddRange(clientUser, techUser, adminUser);

        var technician = new Technician { Id = Guid.NewGuid(), FullName = "Tech", Email = "tech@test.com", IsActive = true, UserId = techUser.Id };
        context.Technicians.Add(technician);

        var category = new Category { Id = 1, Name = "Hardware", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "Printer", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);

        await context.SaveChangesAsync();

        // Create and assign ticket
        var createRequest = new TicketCreateRequest
        {
            Title = "Printer issue",
            Description = "Paper jam",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.Medium
        };

        var ticket = await service.CreateTicketAsync(clientUser.Id, createRequest);
        await service.AssignTechniciansAsync(ticket!.Id, new List<Guid> { techUser.Id }, adminUser.Id);

        // Admin changes status to Redo
        var statusResult = await service.ChangeStatusAsync(ticket.Id, TicketStatus.Redo, adminUser.Id, UserRole.Admin);
        Assert.True(statusResult.Success);

        // Verify canonical status is Redo in database
        var ticketInDb = await context.Tickets.FindAsync(ticket.Id);
        Assert.Equal(TicketStatus.Redo, ticketInDb!.Status);

        // Technician sees Redo (both canonical and display)
        var techTicket = await service.GetTicketAsync(ticket.Id, techUser.Id, UserRole.Technician);
        Assert.NotNull(techTicket);
        Assert.Equal(TicketStatus.Redo, techTicket!.CanonicalStatus);
        Assert.Equal(TicketStatus.Redo, techTicket.DisplayStatus);

        // Admin sees Redo (both canonical and display)
        var adminTicket = await service.GetTicketAsync(ticket.Id, adminUser.Id, UserRole.Admin);
        Assert.NotNull(adminTicket);
        Assert.Equal(TicketStatus.Redo, adminTicket!.CanonicalStatus);
        Assert.Equal(TicketStatus.Redo, adminTicket.DisplayStatus);

        // Client sees Redo as canonical but InProgress as display (role-based mapping)
        var clientTicket = await service.GetTicketAsync(ticket.Id, clientUser.Id, UserRole.Client);
        Assert.NotNull(clientTicket);
        Assert.Equal(TicketStatus.Redo, clientTicket!.CanonicalStatus);
        Assert.Equal(TicketStatus.InProgress, clientTicket.DisplayStatus); // Client sees InProgress instead of Redo
    }
    
    /// <summary>
    /// Test that Solved status is visible to all roles (no mapping needed)
    /// 
    /// PHASE 5 TEST: Verify Solved status is shown as-is to all roles
    /// </summary>
    [Fact]
    public async Task SolvedStatus_VisibleToAllRoles_AsIs()
    {
        var (context, service) = CreateService();

        // Setup
        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", Role = UserRole.Client };
        var techUser = new User { Id = Guid.NewGuid(), Email = "tech@test.com", FullName = "Tech", Role = UserRole.Technician };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin", Role = UserRole.Admin };
        
        context.Users.AddRange(clientUser, techUser, adminUser);

        var technician = new Technician { Id = Guid.NewGuid(), FullName = "Tech", Email = "tech@test.com", IsActive = true, UserId = techUser.Id };
        context.Technicians.Add(technician);

        var category = new Category { Id = 1, Name = "Software", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "Bug", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);

        await context.SaveChangesAsync();

        // Create and assign ticket
        var createRequest = new TicketCreateRequest
        {
            Title = "Bug fix",
            Description = "App crash",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.High
        };

        var ticket = await service.CreateTicketAsync(clientUser.Id, createRequest);
        await service.AssignTechniciansAsync(ticket!.Id, new List<Guid> { techUser.Id }, adminUser.Id);

        // Technician marks as Solved
        var statusResult = await service.ChangeStatusAsync(ticket.Id, TicketStatus.Solved, techUser.Id, UserRole.Technician);
        Assert.True(statusResult.Success);

        // Verify canonical status is Solved in database
        var ticketInDb = await context.Tickets.FindAsync(ticket.Id);
        Assert.Equal(TicketStatus.Solved, ticketInDb!.Status);

        // All roles see Solved (no mapping needed for Solved)
        var techTicket = await service.GetTicketAsync(ticket.Id, techUser.Id, UserRole.Technician);
        Assert.Equal(TicketStatus.Solved, techTicket!.CanonicalStatus);
        Assert.Equal(TicketStatus.Solved, techTicket.DisplayStatus);

        var adminTicket = await service.GetTicketAsync(ticket.Id, adminUser.Id, UserRole.Admin);
        Assert.Equal(TicketStatus.Solved, adminTicket!.CanonicalStatus);
        Assert.Equal(TicketStatus.Solved, adminTicket.DisplayStatus);

        var clientTicket = await service.GetTicketAsync(ticket.Id, clientUser.Id, UserRole.Client);
        Assert.Equal(TicketStatus.Solved, clientTicket!.CanonicalStatus);
        Assert.Equal(TicketStatus.Solved, clientTicket.DisplayStatus);
    }
    
    /// <summary>
    /// Test StatusMappingService.MapStatusForRole directly
    /// 
    /// PHASE 5 TEST: Unit test for mapping function
    /// </summary>
    [Fact]
    public void MapStatusForRole_ReturnsCorrectDisplayStatus()
    {
        // Redo -> InProgress for Client
        Assert.Equal(TicketStatus.InProgress, StatusMappingService.MapStatusForRole(TicketStatus.Redo, UserRole.Client));
        
        // Redo -> Redo for Technician
        Assert.Equal(TicketStatus.Redo, StatusMappingService.MapStatusForRole(TicketStatus.Redo, UserRole.Technician));
        
        // Redo -> Redo for Admin
        Assert.Equal(TicketStatus.Redo, StatusMappingService.MapStatusForRole(TicketStatus.Redo, UserRole.Admin));
        
        // All other statuses unchanged for all roles
        foreach (var status in new[] { TicketStatus.Submitted, TicketStatus.SeenRead, TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Solved })
        {
            Assert.Equal(status, StatusMappingService.MapStatusForRole(status, UserRole.Client));
            Assert.Equal(status, StatusMappingService.MapStatusForRole(status, UserRole.Technician));
            Assert.Equal(status, StatusMappingService.MapStatusForRole(status, UserRole.Admin));
        }
    }

    /// <summary>
    /// Test that SeenRead transition happens on first view by technician/admin.
    /// \n+    /// PHASE 5 TEST: SeenRead workflow status transition
    /// </summary>
    [Fact]
    public async Task SeenRead_Transition_OnFirstTechnicianView()
    {
        var (context, service) = CreateService();

        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", Role = UserRole.Client };
        var techUser = new User { Id = Guid.NewGuid(), Email = "tech@test.com", FullName = "Tech", Role = UserRole.Technician };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin", Role = UserRole.Admin };

        context.Users.AddRange(clientUser, techUser, adminUser);
        context.Technicians.Add(new Technician { Id = Guid.NewGuid(), FullName = "Tech", Email = "tech@test.com", IsActive = true, UserId = techUser.Id });

        var category = new Category { Id = 1, Name = "Network", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "VPN", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);
        await context.SaveChangesAsync();

        var createRequest = new TicketCreateRequest
        {
            Title = "VPN issue",
            Description = "Cannot connect",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.Medium
        };

        var ticket = await service.CreateTicketAsync(clientUser.Id, createRequest);
        Assert.NotNull(ticket);
        Assert.Equal(TicketStatus.Submitted, ticket!.CanonicalStatus);

        // Assign technician so they can access the ticket
        await service.AssignTechniciansAsync(ticket.Id, new List<Guid> { techUser.Id }, adminUser.Id);

        // First technician view should transition status to SeenRead
        var techView = await service.GetTicketAsync(ticket.Id, techUser.Id, UserRole.Technician);
        Assert.NotNull(techView);
        Assert.Equal(TicketStatus.SeenRead, techView!.CanonicalStatus);
        Assert.Equal(TicketStatus.SeenRead, techView.DisplayStatus);

        // Verify database status updated
        var ticketInDb = await context.Tickets.FindAsync(ticket.Id);
        Assert.NotNull(ticketInDb);
        Assert.Equal(TicketStatus.SeenRead, ticketInDb!.Status);
    }

    /// <summary>
    /// Test that client cannot change status to forbidden values (InProgress, Solved, Redo)
    /// </summary>
    [Fact]
    public async Task Client_CannotChangeStatus_ToForbiddenValues()
    {
        var (context, service) = CreateService();

        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", Role = UserRole.Client };
        var category = new Category { Id = 1, Name = "Network", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "VPN", IsActive = true };
        
        context.Users.Add(clientUser);
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);
        await context.SaveChangesAsync();

        var createRequest = new TicketCreateRequest
        {
            Title = "VPN issue",
            Description = "Cannot connect",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.Medium
        };

        var ticket = await service.CreateTicketAsync(clientUser.Id, createRequest);

        // Client tries to change to InProgress - should fail
        var inProgressResult = await service.ChangeStatusAsync(ticket!.Id, TicketStatus.InProgress, clientUser.Id, UserRole.Client);
        Assert.False(inProgressResult.Success);
        Assert.Contains("cannot", inProgressResult.ErrorMessage?.ToLower() ?? "");

        // Client tries to change to Solved - should fail
        var solvedResult = await service.ChangeStatusAsync(ticket.Id, TicketStatus.Solved, clientUser.Id, UserRole.Client);
        Assert.False(solvedResult.Success);

        // Client tries to change to Redo - should fail
        var redoResult = await service.ChangeStatusAsync(ticket.Id, TicketStatus.Redo, clientUser.Id, UserRole.Client);
        Assert.False(redoResult.Success);

        // Verify status remained unchanged
        var ticketInDb = await context.Tickets.FindAsync(ticket.Id);
        Assert.Equal(TicketStatus.Submitted, ticketInDb!.Status);
    }

    /// <summary>
    /// PHASE 6 TEST: Multi-assignee reply synchronization
    /// When Technician2 adds a reply, Technician1 should see it immediately
    /// </summary>
    [Fact]
    public async Task Reply_IsVisibleToAllAssignedTechnicians()
    {
        var (context, service) = CreateService();

        // Setup
        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", Role = UserRole.Client };
        var techUser1 = new User { Id = Guid.NewGuid(), Email = "tech1@test.com", FullName = "Tech1", Role = UserRole.Technician };
        var techUser2 = new User { Id = Guid.NewGuid(), Email = "tech2@test.com", FullName = "Tech2", Role = UserRole.Technician };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin", Role = UserRole.Admin };
        
        context.Users.AddRange(clientUser, techUser1, techUser2, adminUser);

        var technician1 = new Technician { Id = Guid.NewGuid(), FullName = "Tech1", Email = "tech1@test.com", IsActive = true, UserId = techUser1.Id };
        var technician2 = new Technician { Id = Guid.NewGuid(), FullName = "Tech2", Email = "tech2@test.com", IsActive = true, UserId = techUser2.Id };
        context.Technicians.AddRange(technician1, technician2);

        var category = new Category { Id = 1, Name = "Support", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "Help", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);

        await context.SaveChangesAsync();

        // Create and assign ticket to both technicians
        var createRequest = new TicketCreateRequest
        {
            Title = "Support request",
            Description = "Need help",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.Medium
        };

        var ticket = await service.CreateTicketAsync(clientUser.Id, createRequest);
        Assert.NotNull(ticket);
        
        await service.AssignTechniciansAsync(ticket!.Id, new List<Guid> { techUser1.Id, techUser2.Id }, adminUser.Id);

        // Technician2 adds a reply
        var message = await service.AddMessageAsync(ticket.Id, techUser2.Id, "I'm working on this issue", TicketStatus.InProgress);
        Assert.NotNull(message);
        Assert.Equal("Tech2", message!.AuthorName);

        // Technician1 fetches messages - should see the reply from Technician2
        var messagesForTech1 = await service.GetMessagesAsync(ticket.Id, techUser1.Id, UserRole.Technician);
        Assert.Single(messagesForTech1);
        Assert.Equal("I'm working on this issue", messagesForTech1.First().Message);
        Assert.Equal("Tech2", messagesForTech1.First().AuthorName);

        // Admin fetches messages - should see the same reply
        var messagesForAdmin = await service.GetMessagesAsync(ticket.Id, adminUser.Id, UserRole.Admin);
        Assert.Single(messagesForAdmin);
        Assert.Equal("I'm working on this issue", messagesForAdmin.First().Message);

        // Client fetches messages - should see the same reply
        var messagesForClient = await service.GetMessagesAsync(ticket.Id, clientUser.Id, UserRole.Client);
        Assert.Single(messagesForClient);
        Assert.Equal("I'm working on this issue", messagesForClient.First().Message);
    }

    /// <summary>
    /// PHASE 6 TEST: Activity event is logged for reply
    /// </summary>
    [Fact]
    public async Task Reply_CreatesActivityEvent()
    {
        var (context, service) = CreateService();

        // Setup
        var clientUser = new User { Id = Guid.NewGuid(), Email = "client@test.com", FullName = "Client", Role = UserRole.Client };
        var techUser = new User { Id = Guid.NewGuid(), Email = "tech@test.com", FullName = "Tech", Role = UserRole.Technician };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin", Role = UserRole.Admin };
        
        context.Users.AddRange(clientUser, techUser, adminUser);

        var technician = new Technician { Id = Guid.NewGuid(), FullName = "Tech", Email = "tech@test.com", IsActive = true, UserId = techUser.Id };
        context.Technicians.Add(technician);

        var category = new Category { Id = 1, Name = "Support", IsActive = true };
        var subcategory = new Subcategory { Id = 10, CategoryId = 1, Name = "Help", IsActive = true };
        context.Categories.Add(category);
        context.Subcategories.Add(subcategory);

        await context.SaveChangesAsync();

        // Create and assign ticket
        var createRequest = new TicketCreateRequest
        {
            Title = "Support request",
            Description = "Need help",
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Priority = TicketPriority.Medium
        };

        var ticket = await service.CreateTicketAsync(clientUser.Id, createRequest);
        await service.AssignTechniciansAsync(ticket!.Id, new List<Guid> { techUser.Id }, adminUser.Id);

        // Technician adds a reply
        await service.AddMessageAsync(ticket.Id, techUser.Id, "Working on it", TicketStatus.InProgress);

        // Verify activity event was logged
        var activityEvents = await context.TicketActivityEvents
            .Where(e => e.TicketId == ticket.Id && e.EventType == "ReplyAdded")
            .ToListAsync();

        Assert.Single(activityEvents);
        Assert.Equal(techUser.Id, activityEvents[0].ActorUserId);
        Assert.Equal("Technician", activityEvents[0].ActorRole);
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
