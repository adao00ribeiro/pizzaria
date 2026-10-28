using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Infrastructure.Persistence;

/// <summary>
/// Seed de demonstração (docs/09 fase 8 e docs/10 Sprint 7): cria uma pizzaria fictícia com
/// ingredientes, massa base, tamanhos, custos fixos, 10 sabores precificados, bebidas, combo,
/// 1 atendente, 1 garçom, 1 pizzaiolo e ~15 pedidos distribuídos em 30 dias (com baixa real de estoque).
/// Idempotente: se já existir, complementa apenas usuários/pedidos demo ausentes.
/// </summary>
public sealed class DemoSeeder
{
    public const string DemoEmail = "demo@pizzarialucrativa.com";
    public const string DemoAtendenteEmail = "atendente@pizzarialucrativa.com";
    public const string DemoGarcomEmail = "garcom@pizzarialucrativa.com";
    public const string DemoPizzaioloEmail = "pizzaiolo@pizzarialucrativa.com";
    public const string DemoPassword = "Demo123";
    public const string DemoPizzeria = "Pizzaria Lucrativa Demo";

    private readonly IAuthService _auth;
    private readonly UserManager<AppUser> _users;
    private readonly ITenantProvider _tenant;
    private readonly AppDbContext _db;
    private readonly IIngredientService _ingredients;
    private readonly IPreparationService _preparations;
    private readonly IFixedCostService _fixedCosts;
    private readonly IFeeService _fees;
    private readonly IProductService _products;
    private readonly ISizeService _sizes;
    private readonly ITechnicalSheetService _sheets;
    private readonly IComboService _combos;
    private readonly IStockService _stock;
    private readonly IOrderService _orders;
    private readonly ILogger<DemoSeeder> _logger;

    public DemoSeeder(
        IAuthService auth,
        UserManager<AppUser> users,
        ITenantProvider tenant,
        AppDbContext db,
        IIngredientService ingredients,
        IPreparationService preparations,
        IFixedCostService fixedCosts,
        IFeeService fees,
        IProductService products,
        ISizeService sizes,
        ITechnicalSheetService sheets,
        IComboService combos,
        IStockService stock,
        IOrderService orders,
        ILogger<DemoSeeder> logger)
    {
        _auth = auth;
        _users = users;
        _tenant = tenant;
        _db = db;
        _ingredients = ingredients;
        _preparations = preparations;
        _fixedCosts = fixedCosts;
        _fees = fees;
        _products = products;
        _sizes = sizes;
        _sheets = sheets;
        _combos = combos;
        _stock = stock;
        _orders = orders;
        _logger = logger;
    }

    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        var existingOwner = await _users.FindByEmailAsync(DemoEmail);
        var createdCatalog = false;

        if (existingOwner is null)
        {
            await _auth.RegisterAsync(
                new RegisterCommand(DemoEmail, DemoPassword, "Dono Demo", DemoPizzeria), ct);
            existingOwner = await _users.FindByEmailAsync(DemoEmail)
                            ?? throw new InvalidOperationException("Falha ao recuperar o usuário demo.");
            createdCatalog = true;
        }

        _tenant.SetTenant(existingOwner.TenantId);

        try
        {
            await EnsureStaffUsersAsync(existingOwner.TenantId);
            await BackfillDemoProductImagesAsync(ct);
            await EnsureNeighborhoodsAsync(ct);

            if (createdCatalog)
            {
                var ingredients = await CreateIngredientsAsync(ct);
                await CreateInitialStockAsync(ingredients, ct);
                var massa = await CreateMassaAsync(ingredients, ct);
                var sizes = await CreateSizesAsync(ct);
                await CreateFixedCostsAsync(ct);
                await _fees.UpdateAsync(new FeeConfigDto(2.99m, 0m, 8.00m, 60m, 0m), ct);

                var flavors = FlavorRecipes();
                var products = new List<ProductDto>();
                foreach (var (name, imageUrl, _) in flavors)
                    products.Add(await _products.CreateAsync(new ProductInput(name, ProductCategory.Pizza, true, ImageUrl: imageUrl), ct));

                for (var i = 0; i < flavors.Count; i++)
                {
                    foreach (var size in sizes)
                    {
                        var scale = size.Name == "Grande" ? 1.45m : 1m;
                        var items = BuildSheetItems(flavors[i].Toppings, ingredients, massa, scale);
                        await _sheets.CreateAsync(new TechnicalSheetInput(
                            products[i].Id,
                            size.Id,
                            PackagingCost: 0.60m,
                            LossPercentage: null,
                            MarginPercentage: 60m,
                            items), ct);
                    }
                }

                await CreateBeveragesAndComboAsync(products, sizes, ct);
            }

            var ordersCreated = await EnsureDemoOrdersAsync(existingOwner.Id, ct);

            if (createdCatalog || ordersCreated > 0)
            {
                _logger.LogInformation(
                    "Seed demo pronto: {Pizzeria} ({Orders} pedidos criados). Logins: {Owner} | {Atendente} | {Garcom} | {Pizzaiolo} (senha {Password}).",
                    DemoPizzeria, ordersCreated, DemoEmail, DemoAtendenteEmail, DemoGarcomEmail, DemoPizzaioloEmail, DemoPassword);
                return true;
            }

            return false;
        }
        finally
        {
            _tenant.Clear();
        }
    }

    private async Task EnsureStaffUsersAsync(Guid tenantId)
    {
        var staff = new (string Email, string FullName, string Role)[]
        {
            (DemoAtendenteEmail, "Ana Atendente (Demo)", AppRoles.Atendente),
            (DemoGarcomEmail, "Lucas Garçom (Demo)", AppRoles.Garcom),
            (DemoPizzaioloEmail, "Pedro Pizzaiolo (Demo)", AppRoles.Pizzaiolo)
        };

        foreach (var (email, fullName, role) in staff)
        {
            var user = await _users.FindByEmailAsync(email);
            if (user is not null)
                continue;

            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _users.CreateAsync(user, DemoPassword);
            if (createResult.Succeeded)
                await _users.AddToRoleAsync(user, role);
        }
    }

    /// <summary>
    /// Seed idempotente do catálogo de bairros/regiões (IA-1/IA-2). Roda antes dos pedidos
    /// para que o fluxo de delivery possa resolver a região quando aplicável.
    /// </summary>
    private async Task EnsureNeighborhoodsAsync(CancellationToken ct)
    {
        var catalog = new (string Name, NeighborhoodRegion Region)[]
        {
            ("Centro", NeighborhoodRegion.Center),
            ("Sé", NeighborhoodRegion.Center),
            ("Jardins", NeighborhoodRegion.West),
            ("Pinheiros", NeighborhoodRegion.West),
            ("Lapa", NeighborhoodRegion.West),
            ("Butantã", NeighborhoodRegion.West),
            ("Moema", NeighborhoodRegion.South),
            ("Vila Mariana", NeighborhoodRegion.South),
            ("Ipiranga", NeighborhoodRegion.South),
            ("Tatuapé", NeighborhoodRegion.East),
            ("Itaquera", NeighborhoodRegion.East),
            ("Santana", NeighborhoodRegion.North)
        };

        var existing = await _db.Set<Neighborhood>()
            .Select(n => n.Name)
            .ToListAsync(ct);
        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (name, region) in catalog)
        {
            if (!known.Add(name))
                continue;

            _db.Add(new Neighborhood { Name = name, Region = region, IsActive = true });
            added++;
        }

        if (added > 0)
            await _db.SaveChangesAsync(ct);
    }

    private async Task CreateBeveragesAndComboAsync(
        IReadOnlyList<ProductDto> pizzas,
        IReadOnlyList<SizeDto> sizes,
        CancellationToken ct)
    {
        var coca = await _products.CreateAsync(new ProductInput(
            "Coca-Cola 2L", ProductCategory.Beverage, true,
            ImageUrl: "https://images.unsplash.com/photo-1622483767028-3f66f32aef97?w=800&auto=format&fit=crop&q=80",
            PurchasePrice: 6.50m,
            MarkupMultiplier: 2.0m,
            CurrentStock: 24m,
            MinimumStock: 5m), ct);
        var guarana = await _products.CreateAsync(new ProductInput(
            "Guaraná Antarctica 2L", ProductCategory.Beverage, true,
            ImageUrl: "https://images.unsplash.com/photo-1581009146145-b5ef050c2e1e?w=800&auto=format&fit=crop&q=80",
            PurchasePrice: 5.50m,
            MarkupMultiplier: 2.0m,
            CurrentStock: 18m,
            MinimumStock: 5m), ct);

        var grande = sizes.FirstOrDefault(s => s.Name == "Grande") ?? sizes[0];
        var calabresa = pizzas.FirstOrDefault(p => p.Name == "Calabresa") ?? pizzas[0];

        await _combos.CreateAsync(new ComboInput(
            Name: "Combo Família (Grande + Refri 2L)",
            DiscountValue: 5.00m,
            FixedPrice: null,
            Items:
            [
                new ComboItemInputDto(calabresa.Id, grande.Id, 1),
                new ComboItemInputDto(coca.Id, null, 1)
            ]), ct);
    }

    private async Task BackfillDemoProductImagesAsync(CancellationToken ct)
    {
        var defaultImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Margherita"] = "https://images.unsplash.com/photo-1574071318508-1cdbab80d002?w=800&auto=format&fit=crop&q=80",
            ["Calabresa"] = "https://images.unsplash.com/photo-1534308983496-4fabb1a015ee?w=800&auto=format&fit=crop&q=80",
            ["Muçarela"] = "https://images.unsplash.com/photo-1513104890138-7c749659a591?w=800&auto=format&fit=crop&q=80",
            ["Portuguesa"] = "https://images.unsplash.com/photo-1565299624946-b28f40a0ae38?w=800&auto=format&fit=crop&q=80",
            ["Frango com Catupiry"] = "https://images.unsplash.com/photo-1593560708920-61dd98c46a4e?w=800&auto=format&fit=crop&q=80",
            ["Quatro Queijos"] = "https://images.unsplash.com/photo-1573821663912-569905455b1c?w=800&auto=format&fit=crop&q=80",
            ["Bacon"] = "https://images.unsplash.com/photo-1541745537411-b8046dc6d66c?w=800&auto=format&fit=crop&q=80",
            ["Napolitana"] = "https://images.unsplash.com/photo-1604382354936-07c5d9983bd3?w=800&auto=format&fit=crop&q=80",
            ["Pepperoni"] = "https://images.unsplash.com/photo-1628840042765-356cda07504e?w=800&auto=format&fit=crop&q=80",
            ["Vegetariana"] = "https://images.unsplash.com/photo-1576458088443-04a19bb13da6?w=800&auto=format&fit=crop&q=80",
            ["Coca-Cola 2L"] = "https://images.unsplash.com/photo-1622483767028-3f66f32aef97?w=800&auto=format&fit=crop&q=80",
            ["Guaraná Antarctica 2L"] = "https://images.unsplash.com/photo-1581009146145-b5ef050c2e1e?w=800&auto=format&fit=crop&q=80"
        };

        var productsToUpdate = await _db.Set<Product>()
            .Where(p => p.ImageUrl == null || p.ImageUrl == "")
            .ToListAsync(ct);

        var updated = false;
        foreach (var p in productsToUpdate)
        {
            if (defaultImages.TryGetValue(p.Name, out var imgUrl))
            {
                p.ImageUrl = imgUrl;
                updated = true;
            }
        }

        if (updated)
            await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sprint 7: cria ~15 pedidos distribuídos nos últimos 30 dias com estoque baixado
    /// para que o relatório de lucro real e as telas operacionais já abram populados.
    /// </summary>
    private async Task<int> EnsureDemoOrdersAsync(string ownerUserId, CancellationToken ct)
    {
        var existingCount = await _db.Set<Order>().CountAsync(ct);
        if (existingCount >= 10)
            return 0;

        var sizes = await _db.Set<Size>().OrderBy(s => s.DiameterCm).ToListAsync(ct);
        var pizzas = await _db.Set<Product>()
            .Where(p => p.Category == ProductCategory.Pizza && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        if (sizes.Count == 0 || pizzas.Count < 2)
            return 0;

        var grande = sizes.FirstOrDefault(s => s.Name == "Grande") ?? sizes.Last();
        var media = sizes.FirstOrDefault(s => s.Name == "Média") ?? sizes[0];

        // 15 pedidos históricos concluídos ao longo dos últimos 26 dias + 2 ativos hoje na fila
        var specs = new (int DaysAgo, OrderType Type, PaymentMethod Pay, bool IsPublic, string Customer, int FlavorIdxA, int? FlavorIdxB, bool UseGrande)[]
        {
            (26, OrderType.Counter,  PaymentMethod.Pix,  false, "Carlos Mendes",   0, null, true),
            (24, OrderType.Delivery, PaymentMethod.Card, false, "Fernanda Lima",   1, 2,    true),
            (22, OrderType.Pickup,   PaymentMethod.Cash, true,  "Marcos Rocha",    3, null, false),
            (20, OrderType.Delivery, PaymentMethod.Card, true,  "Juliana Alves",   4, 5,    true),
            (18, OrderType.Counter,  PaymentMethod.Pix,  false, "Roberto Silva",   2, null, true),
            (16, OrderType.Delivery, PaymentMethod.Card, false, "Patrícia Gomes",  6, 1,    true),
            (14, OrderType.Pickup,   PaymentMethod.Pix,  true,  "Lucas Ferreira",  7, null, false),
            (12, OrderType.Delivery, PaymentMethod.Card, true,  "Camila Ribeiro",  8, 0,    true),
            (10, OrderType.Counter,  PaymentMethod.Cash, false, "André Costa",     9, null, true),
            (8,  OrderType.Delivery, PaymentMethod.Card, false, "Beatriz Martins", 1, 4,    true),
            (6,  OrderType.Pickup,   PaymentMethod.Pix,  true,  "Thiago Souza",    5, null, true),
            (4,  OrderType.Delivery, PaymentMethod.Card, false, "Larissa Barros",  3, 6,    true),
            (3,  OrderType.Counter,  PaymentMethod.Cash, false, "Gustavo Dias",    2, 8,    false),
            (2,  OrderType.Delivery, PaymentMethod.Pix,  true,  "Helena Castro",   4, null, true),
            (1,  OrderType.Delivery, PaymentMethod.Card, false, "Rafael Nunes",    0, 1,    true),
        };

        var created = 0;
        foreach (var spec in specs)
        {
            var size = spec.UseGrande ? grande : media;
            var pA = pizzas[spec.FlavorIdxA % pizzas.Count];
            IReadOnlyList<CreateOrderFlavorInput> flavorInputs = spec.FlavorIdxB is int idxB
                ? [
                    new CreateOrderFlavorInput(pA.Id, 50m),
                    new CreateOrderFlavorInput(pizzas[idxB % pizzas.Count].Id, 50m)
                  ]
                : [new CreateOrderFlavorInput(pA.Id, 100m)];

            var dto = await _orders.CreateAsync(new CreateOrderInput(
                Type: spec.Type,
                CustomerName: spec.Customer,
                CustomerPhone: "11999998888",
                AddressDistrict: spec.Type == OrderType.Delivery ? "Centro" : null,
                AddressStreet: spec.Type == OrderType.Delivery ? "Rua das Oliveiras" : null,
                AddressNumber: spec.Type == OrderType.Delivery ? "120" : null,
                AddressComplement: null,
                Notes: null,
                PaymentMethod: spec.Pay,
                Discount: 0m,
                Items:
                [
                    new CreateOrderItemInput(size.Id, null, 1, null, flavorInputs)
                ]), ct);

            await _orders.ChangeStatusAsync(dto.Id, OrderStatus.InPreparation, null, ct);
            await _orders.ChangeStatusAsync(dto.Id, OrderStatus.Ready, null, ct);
            if (spec.Type == OrderType.Delivery)
                await _orders.ChangeStatusAsync(dto.Id, OrderStatus.OutForDelivery, null, ct);
            await _orders.ChangeStatusAsync(dto.Id, OrderStatus.Completed, null, ct);

            var orderEntity = await _db.Set<Order>().FirstAsync(o => o.Id == dto.Id, ct);
            var placedAt = DateTime.UtcNow.Date.AddDays(-spec.DaysAgo).AddHours(19);
            orderEntity.PlacedAt = placedAt;
            orderEntity.StartedAt = placedAt.AddMinutes(4);
            orderEntity.ReadyAt = placedAt.AddMinutes(18);
            orderEntity.DeliveredAt = placedAt.AddMinutes(32);
            orderEntity.PlacedByUserId = spec.IsPublic ? null : ownerUserId;
            await _db.SaveChangesAsync(ct);

            created++;
        }

        // Mais 2 pedidos ativos hoje para que a fila da produção (/producao) abra com movimento:
        var activeReceived = await _orders.CreateAsync(new CreateOrderInput(
            Type: OrderType.Delivery,
            CustomerName: "Mariana Oliveira",
            CustomerPhone: "11988887777",
            AddressDistrict: "Jardins",
            AddressStreet: "Av. Paulista",
            AddressNumber: "1000",
            AddressComplement: "Apto 42",
            Notes: "Tirar a cebola da metade Calabresa",
            PaymentMethod: PaymentMethod.Card,
            Discount: 0m,
            Items:
            [
                new CreateOrderItemInput(
                    grande.Id,
                    null,
                    1,
                    "Massa bem assada",
                    [
                        new CreateOrderFlavorInput(pizzas[1 % pizzas.Count].Id, 50m),
                        new CreateOrderFlavorInput(pizzas[2 % pizzas.Count].Id, 50m)
                    ])
            ]), ct);

        var activePrep = await _orders.CreateAsync(new CreateOrderInput(
            Type: OrderType.Counter,
            CustomerName: "João Pedro",
            CustomerPhone: null,
            AddressDistrict: null,
            AddressStreet: null,
            AddressNumber: null,
            AddressComplement: null,
            Notes: null,
            PaymentMethod: PaymentMethod.Pix,
            Discount: 0m,
            Items:
            [
                new CreateOrderItemInput(
                    grande.Id,
                    null,
                    1,
                    null,
                    [new CreateOrderFlavorInput(pizzas[0].Id, 100m)])
            ]), ct);
        await _orders.ChangeStatusAsync(activePrep.Id, OrderStatus.InPreparation, null, ct);

        return created + 2;
    }

    // ---------------------------------------------------------------- catálogo

    private async Task<Dictionary<string, IngredientDto>> CreateIngredientsAsync(CancellationToken ct)
    {
        var data = new (string Name, string Key, MeasureUnit Unit, decimal Qty, decimal Price, decimal Loss, decimal Min)[]
        {
            ("Farinha de trigo", "farinha", MeasureUnit.kg, 1m, 5.00m, 0m, 5m),
            ("Muçarela", "mucarela", MeasureUnit.kg, 1m, 40.00m, 2m, 3m),
            ("Molho de tomate", "molho", MeasureUnit.L, 1m, 8.00m, 0m, 2m),
            ("Calabresa", "calabresa", MeasureUnit.kg, 1m, 30.00m, 0m, 2m),
            ("Presunto", "presunto", MeasureUnit.kg, 1m, 28.00m, 0m, 1m),
            ("Frango desfiado", "frango", MeasureUnit.kg, 1m, 22.00m, 0m, 1m),
            ("Catupiry", "catupiry", MeasureUnit.kg, 1m, 25.00m, 0m, 1m),
            ("Bacon", "bacon", MeasureUnit.kg, 1m, 35.00m, 0m, 1m),
            ("Cebola", "cebola", MeasureUnit.kg, 1m, 6.00m, 0m, 1m),
            ("Azeitona", "azeitona", MeasureUnit.kg, 1m, 18.00m, 0m, 1m),
            ("Queijo parmesão", "parmesao", MeasureUnit.kg, 1m, 45.00m, 0m, 1m),
            ("Tomate", "tomate", MeasureUnit.kg, 1m, 7.00m, 0m, 1m),
            ("Pepperoni", "pepperoni", MeasureUnit.kg, 1m, 38.00m, 0m, 1m),
        };

        var result = new Dictionary<string, IngredientDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data)
        {
            var dto = await _ingredients.CreateAsync(new IngredientInput(
                item.Name, item.Unit, item.Qty, item.Price, item.Loss, item.Min), ct);
            result[item.Key] = dto;
        }

        return result;
    }

    /// <summary>Entrada inicial de 25 unidades de compra por insumo (suporta as baixas históricas sem zerar o estoque).</summary>
    private async Task CreateInitialStockAsync(
        IReadOnlyDictionary<string, IngredientDto> ingredients, CancellationToken ct)
    {
        foreach (var ingredient in ingredients.Values)
        {
            await _stock.CreateMovementAsync(new StockMovementInput(
                ingredient.Id,
                StockMovementType.In,
                Quantity: 25m,
                UnitCost: ingredient.UnitCost,
                Note: "Carga inicial de demonstração"), ct);
        }
    }

    private async Task<PreparationDto> CreateMassaAsync(
        IReadOnlyDictionary<string, IngredientDto> ingredients, CancellationToken ct)
        => await _preparations.CreateAsync(new PreparationInput(
            "Massa artesanal",
            YieldQuantity: 10m,
            YieldUnit: MeasureUnit.un,
            Items:
            [
                new PreparationItemInput(
                    SheetItemType.Ingredient, ingredients["farinha"].Id, null, 1000m, MeasureUnit.g),
                new PreparationItemInput(
                    SheetItemType.Ingredient, ingredients["molho"].Id, null, 200m, MeasureUnit.ml)
            ]), ct);

    private async Task<IReadOnlyList<SizeDto>> CreateSizesAsync(CancellationToken ct)
    {
        var result = new List<SizeDto>();
        foreach (var (name, diameter) in new[] { ("Broto", 25), ("Média", 30), ("Grande", 35) })
            result.Add(await _sizes.CreateAsync(new SizeInput(name, diameter), ct));
        return result;
    }

    private async Task CreateFixedCostsAsync(CancellationToken ct)
    {
        await _fixedCosts.CreateAsync(new FixedCostInput(
            "Aluguel", 3000m, AllocationType.PerPizza, ExpectedPizzasPerMonth: 600,
            ExpectedMonthlyRevenue: null), ct);

        await _fixedCosts.CreateAsync(new FixedCostInput(
            "Energia e gás", 800m, AllocationType.RevenuePercentage, ExpectedPizzasPerMonth: null,
            ExpectedMonthlyRevenue: 30000m), ct);
    }

    // ---------------------------------------------------------------- sabores

    private static IReadOnlyList<(string Name, string ImageUrl, (string Key, decimal Grams)[] Toppings)> FlavorRecipes() =>
    [
        ("Margherita", "https://images.unsplash.com/photo-1574071318508-1cdbab80d002?w=800&auto=format&fit=crop&q=80", [("tomate", 100m)]),
        ("Calabresa", "https://images.unsplash.com/photo-1534308983496-4fabb1a015ee?w=800&auto=format&fit=crop&q=80", [("calabresa", 120m)]),
        ("Muçarela", "https://images.unsplash.com/photo-1513104890138-7c749659a591?w=800&auto=format&fit=crop&q=80", [("mucarela", 80m)]),
        ("Portuguesa", "https://images.unsplash.com/photo-1565299624946-b28f40a0ae38?w=800&auto=format&fit=crop&q=80", [("presunto", 70m), ("azeitona", 30m)]),
        ("Frango com Catupiry", "https://images.unsplash.com/photo-1593560708920-61dd98c46a4e?w=800&auto=format&fit=crop&q=80", [("frango", 100m), ("catupiry", 60m)]),
        ("Quatro Queijos", "https://images.unsplash.com/photo-1573821663912-569905455b1c?w=800&auto=format&fit=crop&q=80", [("parmesao", 60m), ("catupiry", 40m)]),
        ("Bacon", "https://images.unsplash.com/photo-1541745537411-b8046dc6d66c?w=800&auto=format&fit=crop&q=80", [("bacon", 100m)]),
        ("Napolitana", "https://images.unsplash.com/photo-1604382354936-07c5d9983bd3?w=800&auto=format&fit=crop&q=80", [("tomate", 80m), ("parmesao", 40m)]),
        ("Pepperoni", "https://images.unsplash.com/photo-1628840042765-356cda07504e?w=800&auto=format&fit=crop&q=80", [("pepperoni", 100m)]),
        ("Vegetariana", "https://images.unsplash.com/photo-1576458088443-04a19bb13da6?w=800&auto=format&fit=crop&q=80", [("cebola", 60m), ("azeitona", 40m), ("tomate", 60m)]),
    ];

    private static IReadOnlyList<SheetItemInput> BuildSheetItems(
        (string Key, decimal Grams)[] toppings,
        IReadOnlyDictionary<string, IngredientDto> ingredients,
        PreparationDto massa,
        decimal scale)
    {
        var items = new List<SheetItemInput>
        {
            new(SheetItemType.Preparation, null, massa.Id, decimal.Round(1.5m * scale, 2), MeasureUnit.un),
            new(SheetItemType.Ingredient, ingredients["mucarela"].Id, null,
                decimal.Round(150m * scale, 0), MeasureUnit.g),
            new(SheetItemType.Ingredient, ingredients["molho"].Id, null,
                decimal.Round(80m * scale, 0), MeasureUnit.ml)
        };

        items.AddRange(toppings.Select(topping => new SheetItemInput(
            SheetItemType.Ingredient,
            ingredients[topping.Key].Id,
            null,
            decimal.Round(topping.Grams * scale, 0),
            MeasureUnit.g)));

        return items;
    }
}
