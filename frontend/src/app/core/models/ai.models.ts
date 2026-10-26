/**
 * Modelos TypeScript do subsistema de IA do Pizzaria Lucrativa (Sprint IA-4).
 * Espelham rigorosamente os DTOs da API em backend/src/PizzariaLucrativa.Application/DTOs/AiDtos.cs.
 */

export type AnswerBlockType = 'text' | 'chart' | 'table' | 'kpis';

export type ChartKind = 'bar' | 'line' | 'pie' | 'doughnut';

export type ChartValueFormat = 'currency' | 'number' | 'percent';

export type AiMessageRole = 'user' | 'model' | 'tool' | 'system';

export interface ChatSeries {
  name: string;
  data: number[];
}

export interface ChartSpec {
  kind: ChartKind;
  title: string;
  labels: string[];
  series: ChatSeries[];
  valueFormat: ChartValueFormat;
}

export interface TableBlock {
  title?: string | null;
  columns: string[];
  rows: string[][];
}

export interface AnswerKpi {
  label: string;
  value: string;
  valueFormat?: ChartValueFormat;
}

export interface AnswerBlock {
  type: AnswerBlockType;
  text?: string | null;
  chart?: ChartSpec | null;
  table?: TableBlock | null;
  kpis?: AnswerKpi[] | null;
}

export interface AiMeta {
  provider: string;
  model: string;
  periodLabel?: string | null;
  latencyMs: number;
  toolsUsed: string[];
  isFallback: boolean;
}

export interface AiUsage {
  inputTokens: number;
  outputTokens: number;
  estimatedCostUsd: number;
  monthlyInputTokens: number;
  monthlyOutputTokens: number;
  monthlyCostUsd: number;
  monthlyBudgetUsd: number;
  budgetUsedPercentage: number;
  budgetExceeded: boolean;
  isFallback: boolean;
}

export interface ChatRequest {
  message: string;
  conversationId?: string | null;
}

export interface ChatMessage {
  role: AiMessageRole;
  content: string;
  toolsUsed?: string[];
  inputTokens?: number;
  outputTokens?: number;
  estimatedCostUsd?: number;
}

export interface ChatAnswer {
  conversationId: string;
  messageId: string;
  text: string;
  blocks: AnswerBlock[];
  meta: AiMeta;
  usage: AiUsage;
}

export interface ConversationSummary {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messageCount: number;
  isArchived: boolean;
}

export interface ConversationMessage {
  id: string;
  role: AiMessageRole;
  content: string;
  createdAt: string;
  toolsUsed: string[];
  estimatedCostUsd: number;
  blocks?: AnswerBlock[];
}

export interface ConversationDetail {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  isArchived: boolean;
  messages: ConversationMessage[];
}

export interface AiHealth {
  enabled: boolean;
  provider: string;
  model: string;
  historyEnabled: boolean;
  budgetConfigured: boolean;
  apiKeyConfigured: boolean;
}

export interface AiFeedback {
  messageId?: string | null;
  helpful: boolean;
  comment?: string | null;
}
