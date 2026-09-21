/** What one flag serves in one environment. */
export interface FeatureFlagEnvironmentState {
  readonly isOn: boolean;
  /** Null when no variation is pinned, so the value is the calling SDK's own default. */
  readonly value: unknown;
  /** A percentage split serves several variations, so no single value applies. */
  readonly isRollout: boolean;
}

export interface FeatureFlag {
  readonly key: string;
  readonly name: string;
  readonly description: string | null;
  readonly tags: readonly string[];
  readonly environments: Readonly<Record<string, FeatureFlagEnvironmentState>>;
}

export interface FeatureFlagList {
  /** Column order for the page; the backend decides which environments exist. */
  readonly environmentKeys: readonly string[];
  readonly flags: readonly FeatureFlag[];
}
