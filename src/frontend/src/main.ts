import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { desktopInterceptor } from './app/core/desktop-runtime';
import { AppComponent } from './app/app.component';
import { sessionExpiryInterceptor } from './app/core/session-expiry';

bootstrapApplication(AppComponent, { providers: [provideHttpClient(withInterceptors([desktopInterceptor, sessionExpiryInterceptor]))] })
  .catch(error => console.error(error));
