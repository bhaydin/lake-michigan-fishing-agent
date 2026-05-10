import { type FormEvent, type ReactNode, useEffect, useState } from 'react';
import { AlertTriangle, Anchor, CloudSun, LocateFixed, RefreshCw, Search, Waves, Wind } from 'lucide-react';

type ForecastPeriod = {
  name: string;
  startsAt: string;
  endsAt: string;
  windSpeedMph: number;
  windDirection: string;
  waveHeightFeet: number;
  weatherSummary: string;
  hazards: string[];
};

type ReadinessScore = {
  rating: 'Good' | 'Caution' | 'Bad';
  score: number;
  reasons: string[];
  rules: string[];
};

type TripReadinessResponse = {
  location: string;
  zone: string;
  issuedAt: string;
  lastUpdated: string;
  source: string;
  readiness: ReadinessScore;
  periods: ForecastPeriod[];
  marineProducts: MarineForecastProduct[];
};

type MarineForecastProduct = {
  kind: string;
  productCode: string;
  productName: string;
  issuingOffice: string;
  zone: string;
  issuedAt: string;
  source: string;
  text: string;
  periods: ForecastPeriod[];
};

const fallback: TripReadinessResponse = {
  location: 'Lake Michigan near Milwaukee',
  zone: 'LMZ644',
  issuedAt: new Date().toISOString(),
  lastUpdated: new Date().toISOString(),
  source: 'Frontend fallback mock',
  readiness: {
    rating: 'Caution',
    score: 60,
    reasons: ['API is not reachable, so this page is showing bundled demo conditions.'],
    rules: [
      'Good: waves below 2 ft, wind below 15 mph, and no hazards in the next two periods.',
      'Caution: waves from 2 to 3.5 ft, wind from 15 to 20 mph, or non-severe advisory language.',
      'Bad: waves above 3.5 ft, wind above 20 mph, or hazards mentioning small craft, gale, thunder, or storms.'
    ]
  },
  periods: [
    {
      name: 'Today',
      startsAt: new Date().toISOString(),
      endsAt: new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString(),
      windSpeedMph: 14,
      windDirection: 'NW',
      waveHeightFeet: 1.8,
      weatherSummary: 'Partly sunny with a light chop.',
      hazards: []
    },
    {
      name: 'Tonight',
      startsAt: new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString(),
      endsAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
      windSpeedMph: 17,
      windDirection: 'N',
      waveHeightFeet: 2.4,
      weatherSummary: 'Clouds building with scattered showers possible.',
      hazards: ['Monitor nearshore conditions']
    }
  ],
  marineProducts: []
};

const ratingClass: Record<ReadinessScore['rating'], string> = {
  Good: 'good',
  Caution: 'caution',
  Bad: 'bad'
};

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? (import.meta.env.DEV ? 'http://localhost:5000' : '');

export function App() {
  const [forecast, setForecast] = useState<TripReadinessResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [zipCode, setZipCode] = useState('');
  const [query, setQuery] = useState('');

  useEffect(() => {
    const controller = new AbortController();

    async function loadForecast() {
      setLoading(true);
      setError(null);

      try {
        const response = await fetch(`${apiBaseUrl}/api/forecast/trip-readiness${query}`, {
          signal: controller.signal
        });

        if (!response.ok) {
          throw new Error(await readErrorMessage(response));
        }

        setForecast(await readJsonResponse<TripReadinessResponse>(response));
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setError(loadError instanceof Error ? loadError.message : 'Forecast request failed');
          setForecast(fallback);
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    }

    loadForecast();
    return () => controller.abort();
  }, [query]);

  function useBrowserLocation() {
    if (!navigator.geolocation) {
      setError('Browser location is not available.');
      return;
    }

    setLoading(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const { latitude, longitude } = position.coords;
        setQuery(`?lat=${encodeURIComponent(latitude)}&lon=${encodeURIComponent(longitude)}`);
      },
      () => {
        setLoading(false);
        setError('Location permission was denied or unavailable.');
      },
      { enableHighAccuracy: false, timeout: 10000, maximumAge: 10 * 60 * 1000 }
    );
  }

  function useZipCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedZip = zipCode.replace(/\D/g, '');
    if (normalizedZip.length === 0) {
      setQuery('');
      return;
    }

    if (normalizedZip.length !== 5) {
      setError('Enter a 5 digit US ZIP code.');
      return;
    }

    setQuery(`?zip=${encodeURIComponent(normalizedZip)}`);
  }

  const viewModel = forecast ?? fallback;
  const sourceLabel = viewModel.source.startsWith('http') ? 'Live NWS forecast' : viewModel.source;

  return (
    <main className="shell">
      <section className="masthead">
        <div>
          <p className="eyebrow">Lake Michigan fishing planner</p>
          <h1>{viewModel.location}</h1>
          <p className="subtle">{viewModel.zone} marine conditions for salmon and trout trip planning.</p>
        </div>
        <div className={`readiness ${ratingClass[viewModel.readiness.rating]}`}>
          <Anchor aria-hidden="true" size={28} />
          <span>{viewModel.readiness.rating}</span>
          <strong>{viewModel.readiness.score}</strong>
        </div>
      </section>

      {loading && (
        <div className="notice">
          <RefreshCw aria-hidden="true" size={18} />
          Loading latest marine forecast
        </div>
      )}

      {error && (
        <div className="notice warning">
          <AlertTriangle aria-hidden="true" size={18} />
          {error}
        </div>
      )}

      <section className="locator" aria-label="Forecast location">
        <button className="icon-button" type="button" onClick={useBrowserLocation} disabled={loading}>
          <LocateFixed aria-hidden="true" size={19} />
          Browser location
        </button>
        <form className="zip-form" onSubmit={useZipCode}>
          <label htmlFor="zipCode">ZIP</label>
          <input
            id="zipCode"
            inputMode="numeric"
            maxLength={10}
            placeholder="60601"
            value={zipCode}
            onChange={(event) => setZipCode(event.target.value)}
          />
          <button className="icon-button" type="submit" disabled={loading}>
            <Search aria-hidden="true" size={18} />
            Apply
          </button>
        </form>
      </section>

      <section className="summary-grid" aria-label="Marine condition summary">
        <Metric icon={<Waves size={24} />} label="Wave outlook" value={`${maxWave(viewModel.periods).toFixed(1)} ft`} />
        <Metric icon={<Wind size={24} />} label="Wind outlook" value={`${maxWind(viewModel.periods)} mph`} />
        <Metric icon={<CloudSun size={24} />} label="Last updated" value={formatDate(viewModel.lastUpdated)} />
      </section>

      <section className="content-grid">
        <div className="panel">
          <div className="panel-heading">
            <div>
              <h2>Actual forecast</h2>
              <p>{viewModel.periods.length} periods issued {formatDate(viewModel.issuedAt)}</p>
            </div>
            <span className={viewModel.source.startsWith('http') ? 'source-pill live' : 'source-pill'}>
              {sourceLabel}
            </span>
          </div>
          <div className="cards">
            {viewModel.periods.map((period) => (
              <article className="forecast-card" key={`${period.name}-${period.startsAt}`}>
                <div>
                  <h3>{period.name}</h3>
                  <p>{formatWindow(period.startsAt, period.endsAt)}</p>
                </div>
                <dl>
                  <div>
                    <dt>Wind</dt>
                    <dd>{period.windDirection} {period.windSpeedMph} mph</dd>
                  </div>
                  <div>
                    <dt>Waves</dt>
                    <dd>{period.waveHeightFeet.toFixed(1)} ft</dd>
                  </div>
                </dl>
                <p>{period.weatherSummary}</p>
                {period.hazards.length > 0 && (
                  <ul className="hazards">
                    {period.hazards.map((hazard) => <li key={hazard}>{hazard}</li>)}
                  </ul>
                )}
              </article>
            ))}
          </div>
        </div>

        <aside className="panel">
          <h2>Why this score?</h2>
          <ul className="reason-list">
            {viewModel.readiness.reasons.map((reason) => <li key={reason}>{reason}</li>)}
          </ul>
          <h2>Rules</h2>
          <ul className="rules">
            {viewModel.readiness.rules.map((rule) => <li key={rule}>{rule}</li>)}
          </ul>
          <p className="source">Forecast source: {viewModel.source}</p>
        </aside>
      </section>

      {viewModel.marineProducts.length > 0 && (
        <section className="marine-products" aria-label="NOAA marine forecast products">
          {viewModel.marineProducts.map((product) => (
            <article className="panel marine-product" key={`${product.productCode}-${product.zone}`}>
              <div className="panel-heading">
                <div>
                  <h2>{product.kind}</h2>
                  <p>{product.productName} · {product.zone} · issued {formatDate(product.issuedAt)}</p>
                </div>
                <span className="source-pill live">{product.productCode}</span>
              </div>
              <div className="cards">
                {product.periods.map((period) => (
                  <article className="forecast-card compact" key={`${product.kind}-${period.name}-${period.startsAt}`}>
                    <div>
                      <h3>{period.name}</h3>
                      <p>{period.weatherSummary}</p>
                    </div>
                    <dl>
                      <div>
                        <dt>Wind</dt>
                        <dd>{period.windDirection} {period.windSpeedMph} mph</dd>
                      </div>
                      <div>
                        <dt>Waves</dt>
                        <dd>{period.waveHeightFeet.toFixed(1)} ft</dd>
                      </div>
                    </dl>
                    {period.hazards.length > 0 && (
                      <ul className="hazards">
                        {period.hazards.map((hazard) => <li key={hazard}>{hazard}</li>)}
                      </ul>
                    )}
                  </article>
                ))}
              </div>
              <details className="raw-product">
                <summary>Product text</summary>
                <pre>{product.text}</pre>
              </details>
            </article>
          ))}
        </section>
      )}
    </main>
  );
}

function Metric({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <article className="metric">
      {icon}
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function maxWind(periods: ForecastPeriod[]) {
  return Math.max(...periods.slice(0, 2).map((period) => period.windSpeedMph));
}

function maxWave(periods: ForecastPeriod[]) {
  return Math.max(...periods.slice(0, 2).map((period) => period.waveHeightFeet));
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  }).format(new Date(value));
}

function formatWindow(start: string, end: string) {
  return `${formatDate(start)} to ${formatDate(end)}`;
}

async function readErrorMessage(response: Response) {
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('application/json')) {
    return `Forecast request failed with ${response.status}. The server returned ${contentType || 'a non-JSON response'}.`;
  }

  try {
    const body = await response.json() as { error?: string };
    return body.error ?? `Forecast request failed with ${response.status}`;
  } catch {
    return `Forecast request failed with ${response.status}`;
  }
}

async function readJsonResponse<T>(response: Response) {
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('application/json')) {
    throw new Error(`Forecast API returned ${contentType || 'a non-JSON response'} instead of JSON.`);
  }

  return await response.json() as T;
}
