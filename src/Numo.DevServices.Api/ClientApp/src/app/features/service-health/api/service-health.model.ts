/** The outcome of one ping. `downReason` is filled only when the service is down. */
export interface ServiceHealthStatus {
  readonly name: string;
  readonly location: string;
  readonly isUp: boolean;
  readonly statusCode: number | null;
  readonly downReason: string | null;
  readonly responseTimeMs: number;
}

export interface ServiceHealthSnapshot {
  readonly checkedAt: string;
  readonly services: readonly ServiceHealthStatus[];
}
