export interface KeyMappingOption {
  readonly id: string;
  readonly name: string;
}

export interface KeyMappingConnector {
  readonly connectorId: string;
  /** Null when the connector id is missing from the connector list; it cannot be converted against. */
  readonly name: string | null;
  readonly keyFields: readonly string[];
}

/** A connector key travels as its values in keyFields order, never as a dictionary. */
export interface KeyMappingRow {
  readonly connectorKeyValues: readonly string[] | null;
  readonly numoKey: string | null;
  readonly isMapped: boolean;
}

/** One row per submitted input row, in submission order. */
export interface KeyMappingResult {
  readonly keyFields: readonly string[];
  readonly rows: readonly KeyMappingRow[];
  readonly notice: string | null;
}

export interface KeyMappingTarget {
  readonly clientId: string;
  readonly resourceId: string;
  readonly connectorName: string;
  readonly organizationId: string;
}

/** What the picker chooses; the organization id comes from the app-wide tenant id instead. */
export interface KeyMappingPick {
  readonly clientId: string;
  readonly resourceId: string;
  readonly connectorName: string;
  readonly keyFields: readonly string[];
}

/** A complete target plus the chosen connector's key fields, which shape the key input. */
export interface KeyMappingSelection {
  readonly target: KeyMappingTarget;
  readonly keyFields: readonly string[];
}

export type KeyMappingDirection = 'toNumoKeys' | 'toConnectorKeys';
