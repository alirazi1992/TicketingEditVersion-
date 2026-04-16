using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Infrastructure.Data;
using Ticketing.Backend.Infrastructure.Data.Repositories;
using Xunit;

namespace Ticketing.Backend.Tests;

public class CategoryCrudTests
{
    private static (AppDbContext context, ICategoryService service) CreateService()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var categoryRepository = new CategoryRepository(context);
        var unitOfWork = new UnitOfWork(context);
        
        // Create a logger factory for testing
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<CategoryService>();

        var service = new CategoryService(categoryRepository, unitOfWork, logger);

        return (context, service);
    }

    [Fact]
    public async Task CreateCategory_Returns201_WithId()
    {
        // Arrange
        var (context, service) = CreateService();
        var request = new CategoryRequest
        {
            Name = "Test Category",
            Description = "Test description",
            IsActive = true
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0, "Category ID should be greater than 0");
        Assert.Equal("Test Category", result.Name);
        Assert.Equal("Test description", result.Description);
        Assert.True(result.IsActive);
        
        // Verify in database
        var dbCategory = await context.Categories.FindAsync(result.Id);
        Assert.NotNull(dbCategory);
        Assert.Equal("Test Category", dbCategory.Name);
    }

    [Fact]
    public async Task CreateSubcategory_UnderCategory_Returns201_WithId()
    {
        // Arrange
        var (context, service) = CreateService();
        
        // First create a category
        var categoryRequest = new CategoryRequest
        {
            Name = "Parent Category",
            IsActive = true
        };
        var category = await service.CreateAsync(categoryRequest);
        Assert.NotNull(category);
        
        // Now create a subcategory
        var subcategoryRequest = new SubcategoryRequest
        {
            Name = "Test Subcategory",
            Description = "Subcategory description",
            IsActive = true
        };

        // Act
        var result = await service.CreateSubcategoryAsync(category.Id, subcategoryRequest);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0, "Subcategory ID should be greater than 0");
        Assert.Equal("Test Subcategory", result.Name);
        Assert.Equal(category.Id, result.CategoryId);
        Assert.True(result.IsActive);
        
        // Verify in database
        var dbSubcategory = await context.Subcategories.FindAsync(result.Id);
        Assert.NotNull(dbSubcategory);
        Assert.Equal("Test Subcategory", dbSubcategory.Name);
        Assert.Equal(category.Id, dbSubcategory.CategoryId);
    }

    [Fact]
    public async Task GetAllCategories_IncludesNewlyCreatedCategory()
    {
        // Arrange
        var (context, service) = CreateService();
        
        // Create a category
        var request = new CategoryRequest
        {
            Name = "New Category",
            IsActive = true
        };
        var created = await service.CreateAsync(request);

        // Act
        var allCategories = await service.GetAllAsync();

        // Assert
        var categories = allCategories.ToList();
        Assert.Contains(categories, c => c.Id == created.Id && c.Name == "New Category");
    }

    [Fact]
    public async Task GetAdminCategories_IncludesNewlyCreatedCategoryAndSubcategory()
    {
        // Arrange
        var (context, service) = CreateService();
        
        // Create a category with subcategory
        var categoryRequest = new CategoryRequest
        {
            Name = "Admin Category",
            IsActive = true
        };
        var category = await service.CreateAsync(categoryRequest);
        
        var subcategoryRequest = new SubcategoryRequest
        {
            Name = "Admin Subcategory",
            IsActive = true
        };
        var subcategory = await service.CreateSubcategoryAsync(category.Id, subcategoryRequest);

        // Act
        var result = await service.GetAdminCategoriesAsync();

        // Assert
        Assert.True(result.TotalCount >= 1);
        var foundCategory = result.Items.FirstOrDefault(c => c.Id == category.Id);
        Assert.NotNull(foundCategory);
        Assert.Equal("Admin Category", foundCategory.Name);
        Assert.Contains(foundCategory.Subcategories, s => s.Id == subcategory.Id && s.Name == "Admin Subcategory");
    }

    [Fact]
    public async Task CreateCategory_DuplicateName_ThrowsException()
    {
        // Arrange
        var (context, service) = CreateService();
        var request = new CategoryRequest
        {
            Name = "Unique Category",
            IsActive = true
        };
        await service.CreateAsync(request);

        // Act & Assert
        var duplicateRequest = new CategoryRequest
        {
            Name = "Unique Category", // Same name
            IsActive = true
        };
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(duplicateRequest));
    }

    [Fact]
    public async Task CreateSubcategory_NonExistentCategory_ReturnsNull()
    {
        // Arrange
        var (context, service) = CreateService();
        var request = new SubcategoryRequest
        {
            Name = "Orphan Subcategory",
            IsActive = true
        };

        // Act
        var result = await service.CreateSubcategoryAsync(99999, request); // Non-existent category ID

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateCategory_UpdatesSuccessfully()
    {
        // Arrange
        var (context, service) = CreateService();
        var createRequest = new CategoryRequest
        {
            Name = "Original Name",
            Description = "Original description",
            IsActive = true
        };
        var created = await service.CreateAsync(createRequest);

        // Act
        var updateRequest = new CategoryRequest
        {
            Name = "Updated Name",
            Description = "Updated description",
            IsActive = false
        };
        var updated = await service.UpdateAsync(created.Id, updateRequest);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("Updated description", updated.Description);
        Assert.False(updated.IsActive);
        
        // Verify in database
        var dbCategory = await context.Categories.FindAsync(created.Id);
        Assert.Equal("Updated Name", dbCategory!.Name);
    }

    [Fact]
    public async Task DeleteCategory_WithoutTickets_DeletesSuccessfully()
    {
        // Arrange
        var (context, service) = CreateService();
        var request = new CategoryRequest
        {
            Name = "To Be Deleted",
            IsActive = true
        };
        var created = await service.CreateAsync(request);

        // Act
        var deleted = await service.DeleteAsync(created.Id);

        // Assert
        Assert.True(deleted);
        
        // Verify not in database
        var dbCategory = await context.Categories.FindAsync(created.Id);
        Assert.Null(dbCategory);
    }
}
