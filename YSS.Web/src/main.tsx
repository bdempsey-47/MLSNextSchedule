import React from 'react'
import ReactDOM from 'react-dom/client'
import { ApplicationInsights } from '@microsoft/applicationinsights-web'
import App from './App'
import './index.css'

// Initialize Application Insights
const instrumentationKey = import.meta.env.VITE_APP_INSIGHTS_KEY
if (instrumentationKey) {
  const appInsights = new ApplicationInsights({
    config: {
      instrumentationKey,
      enableAutoRouteTracking: true,
      enableRequestHeaderTracking: true,
      enableResponseHeaderTracking: true,
      enableAjaxErrorStatusText: true,
    }
  })
  appInsights.loadAppInsights()
  appInsights.trackPageView()
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)