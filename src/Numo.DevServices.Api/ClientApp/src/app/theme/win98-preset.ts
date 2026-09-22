import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * Windows 98/XP-inspired PrimeNG preset. Every color and bevel lives here as a design
 * token; components and global styles read them back through the generated
 * `var(--p-*)` CSS variables instead of repeating hex values.
 *
 * Bevel recipe (matches the classic 98.css/XP.css technique): a flat 1px dark outline
 * plus two inset box-shadow layers, light top/left + dark bottom/right for a "raised"
 * surface (buttons, popups, the dialog window), reversed for a "sunken" one (text
 * fields, table wells).
 */
const raisedBevel = 'inset 1px 1px 0 {surface.0}, inset -1px -1px 0 {surface.500}';
const sunkenBevel = 'inset 1px 1px 0 {surface.500}, inset -1px -1px 0 {surface.0}';

export const Win98Preset = definePreset(Aura, {
  primitive: {
    borderRadius: { none: '0px', xs: '0px', sm: '0px', md: '0px', lg: '0px', xl: '0px' },
  },
  semantic: {
    primary: {
      50: '#EAF1FC',
      100: '#D3E4F9',
      200: '#A7C9F3',
      300: '#7BADED',
      400: '#4F92E7',
      500: '#316AC5',
      600: '#2A5AA8',
      700: '#22478A',
      800: '#1A366C',
      900: '#13254E',
      950: '#0A246A',
    },
    focusRing: {
      width: '1px',
      style: 'dotted',
      color: '{surface.950}',
      offset: '1px',
      shadow: 'none',
    },
    overlay: {
      select: { shadow: `${raisedBevel}, 2px 2px 0 rgba(0, 0, 0, 0.3)` },
      popover: { shadow: `${raisedBevel}, 2px 2px 0 rgba(0, 0, 0, 0.3)` },
      modal: { shadow: `${raisedBevel}, 3px 3px 0 rgba(0, 0, 0, 0.35)` },
    },
    colorScheme: {
      light: {
        surface: {
          0: '#FFFFFF',
          50: '#ECE9D8',
          100: '#DFDCD3',
          200: '#D4D0C8',
          300: '#ACA899',
          400: '#8B887A',
          500: '#716F64',
          600: '#57554C',
          700: '#3C3B35',
          800: '#242320',
          900: '#141310',
          950: '#000000',
        },
        primary: {
          color: '{primary.500}',
          contrastColor: '#FFFFFF',
          hoverColor: '{primary.600}',
          activeColor: '{primary.700}',
        },
        highlight: {
          background: '{primary.500}',
          focusBackground: '{primary.600}',
          color: '#FFFFFF',
          focusColor: '#FFFFFF',
        },
        text: {
          color: '{surface.950}',
          hoverColor: '{surface.900}',
          mutedColor: '{surface.600}',
          hoverMutedColor: '{surface.700}',
        },
        content: {
          background: '{surface.50}',
          hoverBackground: '{surface.100}',
          borderColor: '{surface.700}',
          color: '{text.color}',
          hoverColor: '{text.hover.color}',
        },
        formField: {
          background: '{surface.0}',
          disabledBackground: '{surface.100}',
          filledBackground: '{surface.50}',
          filledHoverBackground: '{surface.100}',
          filledFocusBackground: '{surface.100}',
          borderColor: '{surface.700}',
          hoverBorderColor: '{surface.700}',
          focusBorderColor: '{primary.color}',
          invalidBorderColor: '#CC0000',
          color: '{surface.950}',
          disabledColor: '{surface.500}',
          placeholderColor: '{surface.500}',
          iconColor: '{surface.500}',
          shadow: sunkenBevel,
        },
        overlay: {
          select: { background: '{surface.0}', borderColor: '{surface.700}', color: '{text.color}' },
          popover: { background: '{surface.50}', borderColor: '{surface.700}', color: '{text.color}' },
          modal: { background: '{surface.50}', borderColor: '{surface.700}', color: '{text.color}' },
        },
      },
    },
  },
  components: {
    dialog: {
      title: { fontSize: '0.9375rem', fontWeight: '700' },
    },
    datatable: {
      colorScheme: {
        light: {
          root: { borderColor: '{surface.700}' },
          row: { stripedBackground: '{surface.50}' },
        },
      },
      headerCell: {
        background: '{surface.50}',
        color: '{surface.950}',
        borderColor: '{surface.700}',
        selectedBackground: '{surface.100}',
      },
      header: {
        background: '{surface.50}',
        borderColor: '{surface.700}',
      },
      row: {
        hoverBackground: '{surface.100}',
      },
      bodyCell: {
        borderColor: '{surface.300}',
      },
    },
    tag: {
      colorScheme: {
        light: {
          primary: { background: '{primary.color}', color: '#FFFFFF' },
          info: { background: '{primary.color}', color: '#FFFFFF' },
          success: { background: '#0A7D0A', color: '#FFFFFF' },
          warn: { background: '#C77F00', color: '#FFFFFF' },
          danger: { background: '#B22222', color: '#FFFFFF' },
          secondary: { background: '{surface.300}', color: '{surface.950}' },
          contrast: { background: '{surface.950}', color: '{surface.0}' },
        },
      },
    },
    message: {
      root: { borderWidth: '2px' },
      closeButton: { borderRadius: '0px' },
      colorScheme: {
        light: {
          info: { background: '{surface.0}', borderColor: '{primary.color}', color: '{primary.color}', shadow: 'none' },
          success: { background: '{surface.0}', borderColor: '#0A7D0A', color: '#0A7D0A', shadow: 'none' },
          warn: { background: '{surface.0}', borderColor: '#C77F00', color: '#C77F00', shadow: 'none' },
          error: { background: '{surface.0}', borderColor: '#B22222', color: '#B22222', shadow: 'none' },
          secondary: { background: '{surface.50}', borderColor: '{surface.700}', color: '{surface.950}', shadow: 'none' },
          contrast: { background: '{surface.950}', borderColor: '{surface.950}', color: '{surface.0}', shadow: 'none' },
        },
      },
    },
    progressbar: {
      root: { background: '{surface.200}' },
      value: { background: '{primary.color}' },
    },
  },
});
