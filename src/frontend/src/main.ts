import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { desktopInterceptor } from './app/core/desktop-runtime';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, { providers: [provideHttpClient(withInterceptors([desktopInterceptor]))] })
  .catch(error => console.error(error));
