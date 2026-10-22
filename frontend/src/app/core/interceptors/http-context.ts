import { HttpContextToken } from '@angular/common/http';

/**
 * Marca uma requisição para que o ErrorInterceptor não abra snackbars
 * (a tela trata o erro localmente, ex.: cálculo ao vivo de precificação).
 */
export const SILENT_ERROR = new HttpContextToken<boolean>(() => false);
