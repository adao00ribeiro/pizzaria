---
name: entity-mapping-pattern
description: Standard pattern and Clean Architecture rules for creating and maintaining EF Core entity mappings (`IEntityTypeConfiguration<T>` / `Map`) in `backend/src/PizzariaLucrativa.Infrastructure/Persistence/Mappings/`. Activate this skill whenever configuring entity persistence, tables, columns, indexes, foreign keys, cascades, or query filters.
---

# Padrão de Mapeamento de Entidades EF Core (`IEntityTypeConfiguration<T>`)

Esta skill estabelece o padrão arquitetural obrigatório para o mapeamento de entidades do Entity Framework Core na camada de persistência da infraestrutura (`PizzariaLucrativa.Infrastructure`).

---

## 1. Princípio da Responsabilidade Única (SRP) e Estrutura de Pastas

1. **Localização Obrigatória**:
   - Todo mapeamento de entidade **DEVE** residir no diretório:
     `backend/src/PizzariaLucrativa.Infrastructure/Persistence/Mappings/`
   - O namespace deve ser:
     `namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;`
2. **Uma Classe e Arquivo por Entidade (SRP)**:
   - **PROIBIDO** agrupar múltiplas configurações em arquivos monolíticos (ex.: `CatalogConfigurations.cs` contendo 10 entidades).
   - Cada entidade possui seu próprio arquivo dedicado no formato `<Entidade>Map.cs`:
     - `IngredientMap.cs`
     - `ProductMap.cs`
     - `PreparationMap.cs`
     - `PreparationItemMap.cs`
     - `FixedCostMap.cs`
     - `SizeMap.cs`
     - `TechnicalSheetMap.cs`
     - `TechnicalSheetItemMap.cs`
     - `ComboMap.cs`
     - `ComboItemMap.cs`
     - `StockMovementMap.cs`
     - `AlertMap.cs`
     - `OrderMap.cs`
     - `OrderItemMap.cs`
     - `OrderItemFlavorMap.cs`
     - `TenantMap.cs`
     - `AppUserMap.cs`
     - `RefreshTokenMap.cs`
     - `FeeConfigMap.cs`

---

## 2. Nomenclatura e Convenção de Classe

Toda classe de mapeamento deve implementar `IEntityTypeConfiguration<T>` e seguir a convenção `<Entidade>Map`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class PrizeMap : IEntityTypeConfiguration<Prize>
{
    public void Configure(EntityTypeBuilder<Prize> builder)
    {
        builder.ToTable("Prizes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Value)
            .IsRequired();

        builder.HasOne(x => x.Round)
            .WithMany(r => r.Prizes)
            .HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

## 3. Diretrizes de Mapeamento

1. **Tabelas e Chaves**:
   - Sempre definir o nome da tabela com `builder.ToTable("NomeTabela")`.
   - Entidades que herdam de `TenantEntity` já possuem PK `Id` (GUID) e `TenantId` com Global Query Filter configurado no `AppDbContext`.
2. **Propriedades e Restrições**:
   - Especifique limites de tamanho com `.HasMaxLength(...)` para strings.
   - Ignore propriedades computadas ou derivadas que não devem ser colunas físicas usando `builder.Ignore(x => x.Propriedade)`.
3. **Relacionamentos e Integridade Referencial**:
   - Explicite o comportamento de exclusão (`DeleteBehavior.Restrict` para não deletar cadastros dependentes acidentalmente, ou `DeleteBehavior.Cascade` para itens filhos/dependentes de agregados como `PreparationItem` e `OrderItem`).
4. **Descoberta Automática**:
   - O `AppDbContext` carrega todos os mappings automaticamente com:
     ```csharp
     builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
     ```

---

## 4. Checklist de Revisão (QA & Code Review)

- [ ] A classe está em `backend/src/PizzariaLucrativa.Infrastructure/Persistence/Mappings/`?
- [ ] A classe é `public sealed class <Entidade>Map : IEntityTypeConfiguration<<Entidade>>`?
- [ ] O arquivo contém apenas uma única classe de mapeamento (SRP)?
- [ ] O nome da tabela e relacionamentos estão explicitamente declarados?
- [ ] `dotnet build backend/PizzariaLucrativa.slnx` compila com 0 erros e os testes passam?
