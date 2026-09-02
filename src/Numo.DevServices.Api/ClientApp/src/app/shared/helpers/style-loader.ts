export class StyleLoader {
  /** Adds a global stylesheet to the document and resolves once it has loaded, or immediately if already present. */
  static addExternalStyles(styleUrl: string): Promise<void> {
    if (document.querySelector(`link[href="${styleUrl}"]`)) {
      return Promise.resolve();
    }

    return new Promise((resolve, reject) => {
      const linkElement = document.createElement('link');
      linkElement.href = styleUrl;
      linkElement.rel = 'stylesheet';
      linkElement.onload = () => resolve();
      linkElement.onerror = () => reject(new Error(`Failed to load style ${styleUrl}`));
      document.head.appendChild(linkElement);
    });
  }
}
