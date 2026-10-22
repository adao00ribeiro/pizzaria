/** Contratos de usuários/papéis — docs/10, Sprint 1. */

export type UserRole = 'owner' | 'atendente' | 'pizzaiolo' | 'garcom';

export const USER_ROLES: readonly UserRole[] = ['owner', 'atendente', 'pizzaiolo', 'garcom'];

export const ROLE_LABELS: Record<string, string> = {
  owner: 'Dono / Administrador',
  atendente: 'Atendente / Caixa',
  pizzaiolo: 'Pizzaiolo',
  garcom: 'Garçom',
};

export const ROLE_DESCRIPTIONS: Record<string, string> = {
  owner: 'Gestão completa, custos, precificação e relatórios.',
  atendente: 'Atendimento presencial e WhatsApp, registro de vendas e caixa.',
  garcom: 'Salão: atende mesas, entrega pedidos e organiza o salão.',
  pizzaiolo: 'Produção: fila da cozinha e preparo das pizzas.',
};

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: UserRole | string;
  isOwner: boolean;
  createdAt: string;
}

export interface CreateUserRequest {
  email: string;
  password: string;
  fullName: string;
  role: UserRole | string;
}

export interface UpdateUserRequest {
  fullName?: string;
  role?: UserRole | string;
}
