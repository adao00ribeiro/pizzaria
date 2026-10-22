using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Tests;

/// <summary>
/// Testes unitários de domínio para a entidade <see cref="Product"/>:
/// instanciação, mutabilidade, persistência de metadados de imagem e invariantes de produto.
/// </summary>
public sealed class ProductEntityTests
{
    [Fact]
    public void Product_CreationWithImageUrl_AssignsPropertiesCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var expectedImageUrl = "https://cdn.pizzarialucrativa.com/images/calabresa-artesanal.webp";

        // Act
        var product = new Product
        {
            TenantId = tenantId,
            Name = "Pizza Calabresa Artesanal",
            Category = ProductCategory.Pizza,
            IsActive = true,
            ImageUrl = expectedImageUrl
        };

        // Assert
        Assert.Equal(tenantId, product.TenantId);
        Assert.Equal("Pizza Calabresa Artesanal", product.Name);
        Assert.Equal(ProductCategory.Pizza, product.Category);
        Assert.True(product.IsActive);
        Assert.Equal(expectedImageUrl, product.ImageUrl);
    }

    [Fact]
    public void Product_ImageUrl_CanBeNullOrUpdated()
    {
        // Arrange
        var product = new Product
        {
            Name = "Pizza Muçarela",
            Category = ProductCategory.Pizza,
            ImageUrl = null
        };

        // Assert initial state
        Assert.Null(product.ImageUrl);

        // Act - assign new image URL
        var updatedUrl = "https://cdn.pizzarialucrativa.com/images/mucarela.jpg";
        product.ImageUrl = updatedUrl;

        // Assert updated state
        Assert.Equal(updatedUrl, product.ImageUrl);

        // Act - clear image URL
        product.ImageUrl = null;

        // Assert cleared state
        Assert.Null(product.ImageUrl);
    }

    [Theory]
    [InlineData("https://images.unsplash.com/photo-1513104890138-7c749659a591")]
    [InlineData("https://storage.googleapis.com/pizzaria/refri-coca.png")]
    [InlineData("http://localhost:5000/uploads/pizza-do-chefe.jpeg")]
    public void Product_WithVariousValidImageUrls_StoresExactValue(string url)
    {
        var product = new Product
        {
            Name = "Item Teste",
            Category = ProductCategory.Other,
            ImageUrl = url
        };

        Assert.Equal(url, product.ImageUrl);
    }
}
