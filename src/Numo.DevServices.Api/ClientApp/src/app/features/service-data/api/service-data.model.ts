// Mirrors Features/ServiceData/ServiceDataContracts.cs. The backend serialises these enums by name,
// so a union of literals fails loudly here when a new member lands rather than rendering blank.
export type FieldKind = 'Text' | 'Guid' | 'Date' | 'DateTime' | 'Number' | 'Boolean' | 'Enum';

export type FilterKind = 'Text' | 'Guid' | 'Date' | 'Boolean' | 'Enum' | 'GuidList';

/** Which nav entry's picker offers a resource, declared per resource rather than inferred. */
export type ResourceSection = 'Personnel' | 'DataIntegration';

export interface ColumnDescriptor {
  readonly key: string;
  readonly label: string;
  readonly kind: FieldKind;
  /** Established per resource by probe: the services silently ignore an order on other columns. */
  readonly isSortable: boolean;
}

export interface FilterDescriptor {
  readonly key: string;
  readonly label: string;
  readonly kind: FilterKind;
  readonly options: readonly string[] | null;
  /** A nested resource's parent-id filter: the request 400s without it, so the page must not fire
   * one until this is set - typically reachable only by following a relation link. */
  readonly isRequired: boolean;
}

export interface ResourceDescriptor {
  readonly key: string;
  readonly label: string;
  readonly serviceName: string;
  readonly section: ResourceSection;
  readonly columns: readonly ColumnDescriptor[];
  readonly filters: readonly FilterDescriptor[];
  readonly requiresTenant: boolean;
  /** Reachable only via a RelationDescriptor from another record - e.g. connection credentials.
   * Hidden from the resource picker. */
  readonly isReachableOnlyByRelation: boolean;
}

export interface RecordLink {
  readonly targetResource: string;
  readonly id: string;
}

export interface Cell {
  readonly value: string | null;
  readonly link: RecordLink | null;
}

export interface ResourceRow {
  readonly id: string;
  readonly deletedAt: string | null;
  /** Positional: one cell per ResourceDescriptor.columns entry, in that order. */
  readonly cells: readonly Cell[];
}

/** No total count exists in either service, so hasMore is all the paging state there is. */
export interface ResourcePage {
  readonly rows: readonly ResourceRow[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly notice: string | null;
}

export interface FieldValue {
  readonly label: string;
  readonly value: string | null;
  readonly kind: FieldKind;
  readonly link: RecordLink | null;
}

export interface RelationDescriptor {
  readonly label: string;
  readonly targetResource: string;
  /** One entry for nearly every relation, two for di-execution-step-dataset (executionId and
   * stepId). Same shape as PageRequest.filters - a relation is just a pre-filled filter set. */
  readonly filters: Readonly<Record<string, string>>;
}

export interface ResourceRecord {
  readonly id: string;
  readonly title: string;
  readonly fields: readonly FieldValue[];
  readonly relations: readonly RelationDescriptor[];
}

export interface ResourceCatalogue {
  readonly resources: readonly ResourceDescriptor[];
}

export interface PageRequest {
  readonly page: number;
  readonly pageSize: number;
  readonly sortColumn: string | null;
  readonly isSortDescending: boolean;
  readonly filters: Readonly<Record<string, string>>;
}
