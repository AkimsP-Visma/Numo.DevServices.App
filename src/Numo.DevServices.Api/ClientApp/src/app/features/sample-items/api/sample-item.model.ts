export interface SampleItem {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
}

export interface CreateSampleItemRequest {
  readonly name: string;
  readonly description: string | null;
}

export interface CreateSampleItemResponse {
  readonly id: string;
}
