# StatusScreenSite

<p align="center"><img src="https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/photo-zoomed.jpg" width=600></p>

Renders a dashboard for display on a cheap phone next to the main monitor. The server runs locally on the desktop, allowing it to display information about the local internet connection quality for the household. The dashboard is then rendered by a browser running on a phone or a tablet. Information currently displayed:

- Ping: the last few minutes' worth of ping times to 8.8.8.8
- Traffic stats: extracted directly from the router
- Time: local time + time in selected timezones
- Weather: using a feed from a local weather station, shows current temperature, highest and lowest temperature over the last two days with a timestamp, sunrise and sunset times, sunset time change per day. Temperature is colored to indicate deviation away from the average temperature at the same time of day over the last 7 days.

![screenshot 1](https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/screenshot1.png)

# Can I use it?

Unlikely unless you're really determined. You'll have to run a local web server. You'll have to configure it through an undocumented XML file. You'll have to update the router and weather service code to suit your router and your local weather station. You'll have to keep it internal as it has no concept of authorised users.

# HTTPing

The service performs an HTTP request to preconfigured sites and records uptime statistics. There are no downtime alerts; the sole purpose is a (detailed) dashboard.

![screenshot 2](https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/screenshot2.png)

Differences to commercial services:

- high probe frequency and instant feedback through the "Recent" chart
- grouped data points display response time percentiles instead of min/max/average
- no alerts
- no UI to speak of
- single source location

## HTTPing detailed charts

The upper chart shows response times colored by percentile, or a fuchsia bar for 100% offline time. The lower chart shows uptime percentage, with red indicating a timeout, yellow an unexpected response. A light green means that 100% of requests were successful within a given group.

### Daily bars, grouped by month
![screenshot 3](https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/screenshot3.png)

### Hourly bars, grouped by day
![screenshot 5](https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/screenshot5.png)

### Hourly bars, grouped by day, different chart settings
![screenshot 4](https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/screenshot4.png)

# Photos

## As installed on my desk
<p align="center"><img src="https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/photo-desk.jpg" width=600></p>

## First usable build
<p align="center"><img src="https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/photo-early.jpg" width=600></p>

## Really bad internet
<p align="center"><img src="https://raw.githubusercontent.com/wiki/rstarkov/StatusScreenSite/photo-bad-internet.jpg" width=600></p>
