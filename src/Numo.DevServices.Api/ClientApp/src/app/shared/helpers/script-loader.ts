export class ScriptLoader {
  private static readonly inflight = new Map<string, Promise<void>>();

  /**
   * Adds a global script to the document and resolves once it has loaded. Concurrent calls for the
   * same URL share one promise; a failed load is evicted so a later call can retry.
   */
  static addExternalScript(scriptUrl: string): Promise<void> {
    const existing = ScriptLoader.inflight.get(scriptUrl);
    if (existing) {
      return existing;
    }

    const loaded = new Promise<void>((resolve, reject) => {
      const scriptElement = document.createElement('script');
      scriptElement.src = scriptUrl;
      scriptElement.onload = () => resolve();
      scriptElement.onerror = () => {
        ScriptLoader.inflight.delete(scriptUrl);
        reject(new Error(`Failed to load script ${scriptUrl}`));
      };
      document.body.appendChild(scriptElement);
    });

    ScriptLoader.inflight.set(scriptUrl, loaded);
    return loaded;
  }
}
