import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

registerLocaleData(localeTr, 'tr-TR');

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
