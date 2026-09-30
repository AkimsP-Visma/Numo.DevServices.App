import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { readProblemDetail } from '../../../../shared/api/problem-details';
import { FeatureFlag, FeatureFlagEnvironmentState } from '../../api/feature-flag.model';
import { FeatureFlagsApiService } from '../../api/feature-flags-api.service';
import { HiddenEnvironmentsStore } from '../../state/hidden-environments.store';

const ROLLOUT_LABEL = 'percentage rollout';
const SDK_DEFAULT_LABEL = 'SDK default';

// Sentinel markers for the "differs across environments" comparison, distinct from anything
// JSON.stringify(value) could ever produce, so a rollout or an absent environment cannot collide
// with a real value that happens to be the string "rollout" or similar.
const ROLLOUT_MARKER = '\u0000rollout';
const SDK_DEFAULT_MARKER = '\u0000sdk-default';
const ABSENT_MARKER = '\u0000absent';

/** Below this many visible environments, "differs" is meaningless - there is nothing to differ
 * from. */
const MIN_ENVIRONMENTS_TO_COMPARE = 2;

/**
 * Fixed-order categorical hues for a multivariate flag's own distinct values (the dataviz skill's
 * default palette, light-mode steps only - this app has no dark theme). Assigned in this order as
 * distinct values are found across one flag's own environments, never cycled: a flag with more
 * values than slots folds the rest into a neutral "Other" color rather than reusing a hue.
 */
const VALUE_COLORS: readonly ValueColor[] = [
  { background: '#2a78d6', foreground: '#ffffff' }, // blue
  { background: '#eb6834', foreground: '#ffffff' }, // orange
  { background: '#1baf7a', foreground: '#ffffff' }, // aqua
  { background: '#eda100', foreground: '#0b0b0b' }, // yellow
  { background: '#e87ba4', foreground: '#0b0b0b' }, // magenta
  { background: '#008300', foreground: '#ffffff' }, // green
  { background: '#4a3aa7', foreground: '#ffffff' }, // violet
  { background: '#e34948', foreground: '#ffffff' }, // red
];

const OTHER_VALUE_COLOR: ValueColor = { background: '#6c757d', foreground: '#ffffff' };

interface ValueColor {
  readonly background: string;
  readonly foreground: string;
}

/** What a cell renders: which visual the value gets, plus whether targeting is off - now the
 * secondary fact, shown only as a small note rather than the cell's main accent. */
interface EnvironmentDisplay {
  readonly label: string;
  readonly kind: 'true' | 'false' | 'discrete' | 'rollout' | 'sdk-default';
  readonly color: ValueColor | null;
  readonly isTargetingOff: boolean;
}

@Component({
  selector: 'app-feature-flags-page',
  imports: [ButtonModule, MessageModule, TableModule, TagModule],
  templateUrl: './feature-flags-page.html',
  styleUrl: './feature-flags-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FeatureFlagsPage {
  private readonly featureFlagsApi = inject(FeatureFlagsApiService);
  protected readonly hiddenEnvironments = inject(HiddenEnvironmentsStore);

  protected readonly flags = signal<FeatureFlag[]>([]);
  protected readonly environmentKeys = signal<string[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly showOnlyDiffering = signal(false);

  /** All declared environments still drive the picker (so a hidden one can be turned back on);
   * only this filtered order drives the table's own columns. */
  protected readonly visibleEnvironmentKeys = computed(() =>
    this.environmentKeys().filter((key) => !this.hiddenEnvironments.isHidden(key)),
  );

  protected readonly canCompareAcrossEnvironments = computed(
    () => this.visibleEnvironmentKeys().length >= MIN_ENVIRONMENTS_TO_COMPARE,
  );

  /** Only rows whose value differs across the currently visible environments - not all
   * environments, since a difference in a hidden column is not one the reader asked to see. */
  protected readonly visibleFlags = computed(() => {
    if (!this.showOnlyDiffering() || !this.canCompareAcrossEnvironments()) {
      return this.flags();
    }

    const visibleKeys = this.visibleEnvironmentKeys();

    return this.flags().filter((flag) => this.hasDifferingValues(flag, visibleKeys));
  });

  protected readonly columnCount = computed(() => this.visibleEnvironmentKeys().length + 2);

  constructor() {
    this.load();
  }

  protected toggleEnvironment(environmentKey: string): void {
    this.hiddenEnvironments.toggle(environmentKey);
  }

  protected setShowOnlyDiffering(checked: boolean): void {
    this.showOnlyDiffering.set(checked);
  }

  protected load(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.featureFlagsApi.getAll().subscribe({
      next: (list) => {
        this.environmentKeys.set([...list.environmentKeys]);
        this.flags.set([...list.flags]);
        this.isLoading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);
        this.errorMessage.set(readProblemDetail(error));
      },
    });
  }

  /** The value carries the accent now, not whether targeting is on - isOn only adds a small note
   * when it is off, since that is the deviation from what a reader expects by default. */
  protected environmentDisplay(flag: FeatureFlag, environmentKey: string): EnvironmentDisplay | null {
    const state = flag.environments[environmentKey];

    if (!state) {
      return null;
    }

    const isTargetingOff = !state.isOn;

    if (state.isRollout) {
      return { label: ROLLOUT_LABEL, kind: 'rollout', color: null, isTargetingOff };
    }

    if (state.value === null || state.value === undefined) {
      return { label: SDK_DEFAULT_LABEL, kind: 'sdk-default', color: null, isTargetingOff };
    }

    if (typeof state.value === 'boolean') {
      return {
        label: state.value ? 'True' : 'False',
        kind: state.value ? 'true' : 'false',
        color: null,
        isTargetingOff,
      };
    }

    const label = typeof state.value === 'string' ? state.value : JSON.stringify(state.value);

    return { label, kind: 'discrete', color: this.discreteValueColor(flag, environmentKey), isTargetingOff };
  }

  /** Where in this flag's own fixed hue order the current environment's value falls - scoped to
   * one flag's row and to the currently visible columns, not global, so an unrelated flag's
   * variation 0 does not borrow this one's color for a coincidence that carries no shared
   * meaning, and a hidden environment's value does not consume a slot no one sees. */
  private discreteValueColor(flag: FeatureFlag, environmentKey: string): ValueColor {
    const distinctLabels: string[] = [];

    for (const key of this.visibleEnvironmentKeys()) {
      const label = this.discreteLabel(flag.environments[key]);

      if (label !== null && !distinctLabels.includes(label)) {
        distinctLabels.push(label);
      }
    }

    const index = distinctLabels.indexOf(this.discreteLabel(flag.environments[environmentKey])!);

    return index >= 0 && index < VALUE_COLORS.length ? VALUE_COLORS[index] : OTHER_VALUE_COLOR;
  }

  /** Null for anything that is not a concrete, colorable value: absent, a rollout, an SDK default
   * or a boolean (booleans get their own true/false colors, not a slot in this order). */
  private discreteLabel(state: FeatureFlagEnvironmentState | undefined): string | null {
    if (!state || state.isRollout || state.value === null || state.value === undefined || typeof state.value === 'boolean') {
      return null;
    }

    return typeof state.value === 'string' ? state.value : JSON.stringify(state.value);
  }

  /** Whether this flag serves more than one distinct outcome across the given environments -
   * value, rollout-ness and SDK-default-ness all count, but targeting on/off does not, matching
   * the same "value is what matters" read the coloring above already gives the cell. */
  private hasDifferingValues(flag: FeatureFlag, environmentKeys: readonly string[]): boolean {
    const distinct = new Set(environmentKeys.map((key) => this.canonicalValue(flag.environments[key])));

    return distinct.size > 1;
  }

  private canonicalValue(state: FeatureFlagEnvironmentState | undefined): string {
    if (!state) {
      return ABSENT_MARKER;
    }

    if (state.isRollout) {
      return ROLLOUT_MARKER;
    }

    if (state.value === null || state.value === undefined) {
      return SDK_DEFAULT_MARKER;
    }

    return JSON.stringify(state.value);
  }
}
