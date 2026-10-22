/** Contrato POST /auth/register | /auth/login | /auth/refresh (docs/07 §1) */

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  /** Validade do access token em segundos */
  expiresIn: number;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  pizzeriaName: string;
}
