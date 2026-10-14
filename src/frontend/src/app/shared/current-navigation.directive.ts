import { Directive, input } from '@angular/core';

/** Mirrors the existing active state without introducing another navigation model. */
@Directive({ selector: 'button[scpCurrent]', host: { '[class.active]': 'scpCurrent()', '[attr.aria-current]': 'scpCurrent() ? "page" : null' } })
export class CurrentNavigationDirective { readonly scpCurrent = input(false); }
