# Release Notes & Checklist de Go-Live — Assistente de IA (Google Gemini)

> **Versão:** 1.0.0 (Release Candidate)  
> **Data:** 26 de Outubro de 2026  
> **Status:** ✅ APROVADO PARA PRODUÇÃO (Go-Live)  
> **Feature:** Assistente de IA sobre os Dados Operacionais (Sprints IA-1 a IA-5)

---

## 1. Resumo da Entrega

O **Pizzaria Lucrativa** agora conta com um **Assistente de IA conversacional integrado ao Google Gemini (Gemini 1.5 Flash)**, operando via **Function Calling sobre a camada semântica de métricas do backend**.

### Principais Capacidades Entregues:
1. **Perguntas em Linguagem Natural**: Análise de lucro bruto/líquido consolidado e diário, faturamento, ticket médio, margem percentual, comparativo entre períodos, ranking de produtos mais lucrativos, distribuição de vendas por canal e mapa de calor de horários.
2. **Visualizações Gráficas Automáticas**: Gráficos de barras e linha (`ChartSpecDto` renderizado no frontend via `ng2-charts` / Chart.js), tabelas e cards de KPI.
3. **Análise por Bairros e Regiões**: Mapeamento de pedidos por bairros cadastrados no catálogo com snapshot na conclusão do pedido.
4. **Histórico Persistido por Tenant**: Sessões de conversa persistidas (`AiConversation`, `AiMessage`), com título automático, listagem, recuperação de contexto e exclusão segura pelo proprietário.
5. **Governança e Proteção de Custos**:
   - Controle estrito de orçamento mensal (`Ai:Budget:MonthlyUsdPerTenant = $5.00`), com bloqueio pré-chamada caso o teto seja atingido.
   - Alerta antecipado quando o consumo atinge 80% do teto mensal.
   - Cache em memória para perguntas repetidas idênticas (5 minutos).
   - Retenção automática de 90 dias com job de limpeza em background (`AiHistoryCleanupService`).
   - Rollout gradual configurável (`Ai:RolloutPercentage`) e Kill Switch instantâneo (`Ai:Enabled`).
6. **Segurança e Privacidade (LGPD)**:
   - Restrição absoluta ao perfil `owner` (`OwnerOnly`); atendente e pizzaiolo recebem `403 Forbidden`.
   - Proteção rigorosa contra vazamento de PII (telefones, CPFs e endereços nunca alimentam o LLM nem as tools).
   - Sanitização contra Prompt Injection e tags XML invasoras (`WrapUserQuery`).
   - Acesso multi-tenant estritamente isolado pelo claim do JWT. A IA nunca recebe nem envia `tenantId`.

---

## 2. Checklist Executável de Go-Live

| Etapa | Ação | Verificação | Responsável | Status |
|---|---|---|---|---|
| **1. Infra / DB** | Executar migration EF Core `20261026161958_AddNeighborhoodsAndAiHistory` | Tabelas `Neighborhoods`, `AiConversations`, `AiMessages`, `AiUsageRecords` criadas com índices | `backend-specialist` | ✅ Validado |
| **2. Variáveis** | Configurar `Ai__ApiKey` no cofre de segredos / environment da nuvem | Não commitar nem expor nos logs | `orchestrator` | ✅ Validado |
| **3. Configuração** | Definir `Ai__Enabled=true`, `Ai__Model=gemini-1.5-flash`, `Ai__RolloutPercentage=100` | Endpoint `/api/v1/ai/health` responde status OK | `backend-specialist` | ✅ Validado |
| **4. Orçamento** | Ajustar `Ai__Budget__MonthlyUsdPerTenant=5.00` e `Ai__Budget__Enforce=true` | Endpoint `/api/v1/ai/usage` exibe teto correto | `orchestrator` | ✅ Validado |
| **5. Suíte CI** | Executar suíte completa (`dotnet test backend/PizzariaLucrativa.slnx` + `npm run build`) | 169 testes verdes, build Angular sem erros | `qa-test-specialist` | ✅ 100% Verde |
| **6. UAT** | Validação funcional com o proprietário no frontend `/ia` | Perguntas de lucro, região, produto, histórico e exclusão | `qa-test-specialist` | ✅ Aprovado |

---

## 3. Variáveis de Ambiente e Configurações de Produção

```json
{
  "Ai": {
    "Enabled": true,
    "RolloutPercentage": 100,
    "Provider": "Gemini",
    "BaseUrl": "https://generativelanguage.googleapis.com",
    "Model": "gemini-1.5-flash",
    "ApiKey": "${AI_GEMINI_API_KEY}",
    "MaxOutputTokens": 1024,
    "TimeoutSeconds": 25,
    "MaxToolRounds": 4,
    "MaxHistoryMessages": 12,
    "CacheMinutes": 5,
    "Budget": {
      "MonthlyUsdPerTenant": 5.00,
      "Enforce": true,
      "AlertPercentage": 80
    },
    "History": {
      "Enabled": true,
      "RetentionDays": 90,
      "MaxMessagesPerConversation": 50,
      "StoreToolResults": false
    }
  }
}
```

---

## 4. Runbook Operacional: Observabilidade, Alertas e Rollback

### Monitoramento e Logs Estruturados (Serilog)
As interações com a IA geram entradas estruturadas com a chave `AI interaction completed`:
```
AI interaction completed: TenantId={TenantId}, ConversationId={ConversationId}, Provider=Gemini, Model=gemini-1.5-flash, ToolsUsed={ToolsUsed}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, LatencyMs={LatencyMs}, EstimatedCostUsd={EstimatedCostUsd}, IsFallback={IsFallback}, HistoryEnabled=true
```

### Métricas e Alertas
1. **Alerta de Latência Elevada**: Disparado se `LatencyMs > 10.000` (10 segundos).
2. **Alerta de Orçamento**: Disparado se `BudgetUsedPercentage >= 80%`. Bloqueio automático em 100%.
3. **Alerta de Fallback / Falha da Provedora**: Registrado se Gemini retornar 429, 5xx ou timeout. Resposta devolve mensagem amigável sem inventar dados.
4. **Alerta de Falha no Job de Retenção**: Log de erro registrado pelo `AiHistoryCleanupService`.

### Procedimento de Rollback Imediato (Kill Switch)
Caso seja necessário interromper o subsistema de IA sem interromper a operação de pedidos da pizzaria:
1. Altere a variável de ambiente `Ai__Enabled` para `false` no painel da infraestrutura (ou via config reload).
2. O sistema entra em modo fallback seguro em < 1 minuto, sem necessidade de redeploy da aplicação.
3. Todas as rotas de pedidos, cozinha, estoque e relatórios tradicionais continuam funcionando normalmente.

---

## 5. Backlog Pós-Release (Próximas Evoluções)

1. **Streaming de Respostas (Server-Sent Events / SSE)**: Exibição de resposta palavra por palavra no chat.
2. **Notificações e Assistente via WhatsApp**: Canal de consulta rápida para o proprietário via Evolution API.
3. **Suporte Multi-Provedor Híbrido**: Fallback automático para Google Cloud Vertex AI ou Claude 3.5 Sonnet caso a Gemini Developer API oscile.
4. **Insights Proativos Diários**: Envio matinal do resumo de vendas e sugestões automáticas de reposição de insumos.
