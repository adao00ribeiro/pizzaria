import { AlertStatus, AlertType } from './common.models';

/** GET /alerts — item da listagem */
export interface Alert {
  id: string;
  type: AlertType;
  message: string;
  createdAt: string;
  status: AlertStatus;
}

/** GET /alerts/count — badge do menu */
export interface AlertCount {
  count: number;
}
