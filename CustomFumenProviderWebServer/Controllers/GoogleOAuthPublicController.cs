#nullable enable

using System.Text.Encodings.Web;
using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.AspNetCore.Mvc;

namespace CustomFumenProviderWebServer.Controllers
{
    /// <summary>
    /// Public pages used for Google OAuth brand / sensitive-scope verification.
    /// These pages must remain accessible without login and must describe the
    /// application purpose, data usage, privacy policy and terms of service.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("oauth/google/public")]
    public sealed class GoogleOAuthPublicController : ControllerBase
    {
        private const string LastUpdated = "November 3, 2026";

        private readonly GmailOptions options;

        public GoogleOAuthPublicController(GmailOptions options)
        {
            this.options = options;
        }

        [HttpGet("")]
        [HttpGet("home")]
        public IActionResult Home()
        {
            var appName = H(options.PublicAppName);
            var contact = H(options.PublicContactEmail);

            var body = $$"""
                <section class="hero">
                  <p class="eyebrow">Public information page</p>
                  <h1>{{appName}}</h1>
                  <p class="lead">A self-hosted email notification component for custom rhythm-game chart submissions.</p>
                  <p class="notice">This page is publicly accessible. No Google sign-in or user account is required to view it.</p>
                </section>

                <section>
                  <h2>What this application does</h2>
                  <p><strong>{{appName}}</strong> is part of the self-hosted <strong>CustomFumenProviderWebServer</strong> platform. The platform lets chart authors upload custom rhythm-game chart packages and lets an administrator review and publish them.</p>
                  <p>The Gmail integration sends <strong>transactional notification emails</strong> to chart authors, reviewers, or the administrator when an operational event occurs, such as a review result, a publication status change, or an administrator-triggered test message. It is not a marketing, bulk-email, newsletter, or advertising service.</p>
                </section>

                <section>
                  <h2>Who uses it</h2>
                  <p>Only the administrator of a self-hosted instance connects a Google account. There is no public sign-up, no user account creation, and no third-party login for visitors.</p>
                </section>

                <section>
                  <h2>How Google user data is used</h2>
                  <div class="table-wrap">
                    <table>
                      <thead>
                        <tr><th>Scope</th><th>Purpose</th></tr>
                      </thead>
                      <tbody>
                        <tr><td><code>openid</code></td><td>Identify the Google account that authorized the service.</td></tr>
                        <tr><td><code>email</code></td><td>Display and store the authorized sender email address.</td></tr>
                        <tr><td><code>https://www.googleapis.com/auth/gmail.send</code></td><td>Send transactional notification emails from the authorized Gmail account to recipients selected by the administrator.</td></tr>
                      </tbody>
                    </table>
                  </div>
                  <p>We do not read, search, modify, or delete Gmail messages. We do not access Google Contacts. We do not sell or share Google user data with advertisers or data brokers.</p>
                </section>

                <section>
                  <h2>Data storage and security</h2>
                  <p>The OAuth refresh token is encrypted at rest with ASP.NET Core Data Protection. Encryption keys are stored on the server's persistent data volume, and access is restricted to the server administrator. Tokens are never displayed in the web interface or written to logs. The authorized sender address and limited operational logs (timestamp, recipient, message ID and error code) may be stored for operation and troubleshooting.</p>
                </section>

                <section>
                  <h2>AI and prohibited content</h2>
                  <p>{{appName}} does not use Google user data to train AI models, does not generate images, and does not create or distribute AI-generated non-consensual intimate imagery (NCI). The service is an email notification component only.</p>
                </section>

                <section>
                  <h2>Review documents</h2>
                  <ul class="documents">
                    <li><a href="/oauth/google/public/purpose">Application purpose and OAuth scope justification</a></li>
                    <li><a href="/oauth/google/public/privacy">Privacy Policy</a></li>
                    <li><a href="/oauth/google/public/terms">Terms of Service</a></li>
                  </ul>
                </section>

                <section lang="zh-CN">
                  <h2>中文摘要</h2>
                  <p>本服务是自建谱面投稿平台的通知组件，仅在管理员授权后使用 Gmail 发信权限发送审核结果、发布状态等事务性通知。服务不读取、不修改、不删除 Gmail 邮件，不访问通讯录，不出售 Google 用户数据，也不使用 Google 用户数据训练 AI 或生成图像。公开页面无需登录即可访问。</p>
                </section>

                <section>
                  <h2>Contact</h2>
                  <p>Questions about this application or its Google API use: <a href="mailto:{{contact}}">{{contact}}</a>.</p>
                </section>
                """;

            return Page("Home", $"{options.PublicAppName} is a self-hosted transactional email notification component for custom rhythm-game chart submissions.", body);
        }

        [HttpGet("purpose")]
        public IActionResult Purpose()
        {
            var appName = H(options.PublicAppName);
            var contact = H(options.PublicContactEmail);

            var body = $$"""
                <section class="hero">
                  <p class="eyebrow">Google OAuth verification</p>
                  <h1>Application purpose and OAuth scope justification</h1>
                  <p class="lead">This page explains why {{appName}} requests Google OAuth access and how the service uses Google user data.</p>
                </section>

                <section>
                  <h2>Application summary</h2>
                  <div class="table-wrap">
                    <table>
                      <tbody>
                        <tr><th>Application name</th><td>{{appName}}</td></tr>
                        <tr><th>Application type</th><td>Web application (self-hosted ASP.NET Core service)</td></tr>
                        <tr><th>Operator</th><td>The administrator of a self-hosted CustomFumenProviderWebServer instance</td></tr>
                        <tr><th>Users</th><td>One administrator per instance; no public sign-up or third-party login</td></tr>
                        <tr><th>Support contact</th><td><a href="mailto:{{contact}}">{{contact}}</a></td></tr>
                      </tbody>
                    </table>
                  </div>
                </section>

                <section>
                  <h2>Why the scopes are needed</h2>
                  <div class="table-wrap">
                    <table>
                      <thead>
                        <tr><th>Scope</th><th>Justification</th></tr>
                      </thead>
                      <tbody>
                        <tr><td><code>openid</code></td><td>Stable identification of the Google account that authorized the notification component.</td></tr>
                        <tr><td><code>email</code></td><td>Show and store the authorized sender address so the administrator can confirm which account will send notifications.</td></tr>
                        <tr><td><code>https://www.googleapis.com/auth/gmail.send</code></td><td>Send transactional notification emails only. The service does not request Gmail read, modify, delete, metadata, contacts, calendar, or drive access.</td></tr>
                      </tbody>
                    </table>
                  </div>
                </section>

                <section>
                  <h2>Data flow</h2>
                  <ol class="steps">
                    <li>The administrator opens the server's OAuth start page and authorizes the Google account.</li>
                    <li>Google returns an authorization code; the server exchanges it for an access token and a refresh token.</li>
                    <li>The refresh token is encrypted at rest. It is used only to obtain short-lived access tokens for Gmail sending.</li>
                    <li>When a notification is needed, the server constructs the email and calls the Gmail API send endpoint.</li>
                    <li>No Gmail mailbox content is read, modified, or stored by the service.</li>
                  </ol>
                </section>

                <section>
                  <h2>Limited Use commitments</h2>
                  <ul>
                    <li>Google user data is used only to provide and improve the email notification feature described on this page.</li>
                    <li>Google user data is not sold, rented, or shared with advertisers or data brokers.</li>
                    <li>Google user data is not used for advertising, profiling, or AI model training.</li>
                    <li>Google user data is not transferred to third parties except as necessary to provide the feature, comply with law, or as part of a merger or acquisition with notice.</li>
                    <li>Humans do not read Gmail mailbox content because the service does not request access to mailbox content.</li>
                  </ul>
                </section>

                <section>
                  <h2>Prohibited content and uses</h2>
                  <p>The service must not be used for spam, unsolicited bulk email, harassment, unlawful activity, deceptive messages, or AI-generated non-consensual intimate imagery (NCI). It is not an AI image generation service and does not create or distribute such content.</p>
                </section>

                <section>
                  <h2>Related documents</h2>
                  <ul class="documents">
                    <li><a href="/oauth/google/public/privacy">Privacy Policy</a></li>
                    <li><a href="/oauth/google/public/terms">Terms of Service</a></li>
                    <li><a href="/oauth/google/public/">Public home page</a></li>
                  </ul>
                </section>
                """;

            return Page("Application purpose", $"Purpose and scope justification for the {options.PublicAppName} Google OAuth integration.", body);
        }

        [HttpGet("privacy")]
        public IActionResult Privacy()
        {
            var appName = H(options.PublicAppName);
            var contact = H(options.PublicContactEmail);

            var body = $$"""
                <section class="hero">
                  <p class="eyebrow">Privacy Policy</p>
                  <h1>Privacy Policy</h1>
                  <p class="lead">How {{appName}} handles Google user data and email notification data.</p>
                  <p class="meta">Effective date: {{LastUpdated}} &middot; Last updated: {{LastUpdated}}</p>
                </section>

                <section>
                  <h2>1. Scope</h2>
                  <p>{{appName}} ("the Service") is a self-hosted email notification component of a CustomFumenProviderWebServer instance operated by the administrator of that instance ("we", "us"). This policy applies to the Google OAuth integration and the Gmail sending feature. It does not cover third-party websites or services that we do not control.</p>
                </section>

                <section>
                  <h2>2. Google user data we access</h2>
                  <p>When the administrator authorizes the Service with Google, we receive:</p>
                  <ul>
                    <li>The Google account email address, through the <code>email</code> scope and the OpenID Connect ID token.</li>
                    <li>An OAuth refresh token for the <code>gmail.send</code> scope.</li>
                    <li>Short-lived OAuth access tokens derived from the refresh token.</li>
                  </ul>
                  <p>We do not receive or store the Google account password.</p>
                </section>

                <section>
                  <h2>3. How we use Google user data</h2>
                  <ul>
                    <li><code>openid</code> and <code>email</code>: identify the authorized sending account and display it to the administrator.</li>
                    <li><code>gmail.send</code>: send transactional notification emails from the authorized account to recipients selected by the administrator. Examples include review-status notifications, operational notices, and administrator test messages.</li>
                  </ul>
                  <p>We do not use Google user data to read, search, modify, or delete Gmail messages; to access Google Contacts; to advertise; to profile users; or to train AI models.</p>
                </section>

                <section>
                  <h2>4. Email content and recipients</h2>
                  <p>The Service constructs email content and recipient lists supplied by the administrator or by the chart-review workflow. The Gmail integration transmits the message to Google's Gmail API and does not maintain a separate archive of the message body. Operational logs may contain the recipient address, timestamp, Gmail message ID, and error code.</p>
                </section>

                <section>
                  <h2>5. Storage and security</h2>
                  <ul>
                    <li>The refresh token is encrypted at rest using ASP.NET Core Data Protection.</li>
                    <li>Encryption keys are stored in the Service's persistent data directory.</li>
                    <li>Access is restricted to the server administrator.</li>
                    <li>Tokens are never displayed in the web interface or written to logs.</li>
                    <li>Data in transit is protected with HTTPS.</li>
                  </ul>
                  <p>No method of storage or transmission is completely secure, but we take reasonable measures to protect the data.</p>
                </section>

                <section>
                  <h2>6. Sharing</h2>
                  <p>We do not sell, rent, or trade Google user data. We transmit email content and authentication data to Google's Gmail API solely to provide the email-sending feature. We may disclose data if required by law or to protect rights and safety. We do not share Google user data with advertisers or data brokers.</p>
                </section>

                <section>
                  <h2>7. Retention</h2>
                  <ul>
                    <li><strong>Refresh token:</strong> retained until the administrator revokes access, the Service replaces it, or it is no longer needed. When Google reports <code>invalid_grant</code>, the Service deletes the stored token.</li>
                    <li><strong>Authorized sender email:</strong> retained until access is revoked or deletion is requested.</li>
                    <li><strong>Operational logs:</strong> retained for a limited period for security and troubleshooting.</li>
                    <li><strong>Email content:</strong> not archived by the Gmail integration after sending.</li>
                    <li><strong>Chart data and uploader contact information:</strong> may be retained by the separate chart-hosting features; contact us to request deletion.</li>
                  </ul>
                </section>

                <section>
                  <h2>8. Your choices and deletion</h2>
                  <ul>
                    <li>Revoke the application's access at <a href="https://myaccount.google.com/permissions">https://myaccount.google.com/permissions</a>.</li>
                    <li>Request deletion of the stored refresh token, authorized sender address, or related logs by contacting <a href="mailto:{{contact}}">{{contact}}</a>. Include the authorized Google account email in your request.</li>
                    <li>After revocation or deletion, email sending stops.</li>
                  </ul>
                </section>

                <section>
                  <h2>9. Children</h2>
                  <p>The Service is not directed to children under 13, or the minimum age required in the relevant jurisdiction. We do not knowingly collect personal data from children.</p>
                </section>

                <section>
                  <h2>10. International processing</h2>
                  <p>Google API processing may occur in countries other than yours and is governed by Google's terms. The self-hosted server is operated in the jurisdiction chosen by its administrator.</p>
                </section>

                <section>
                  <h2>11. Changes</h2>
                  <p>We may update this policy from time to time. The effective date at the top of this page will change when we do. Material changes will be posted on this page.</p>
                </section>

                <section>
                  <h2>12. Contact</h2>
                  <p>Privacy questions and deletion requests: <a href="mailto:{{contact}}">{{contact}}</a>.</p>
                </section>

                <section>
                  <h2>13. Google API Services User Data Policy</h2>
                  <p>Our use of Google user data complies with the <a href="https://developers.google.com/terms/api-services-user-data-policy">Google API Services User Data Policy</a>, including the Limited Use requirements:</p>
                  <ul>
                    <li>We use Google user data only to provide or improve the email notification feature described on this site.</li>
                    <li>We do not transfer Google user data to third parties except as necessary to provide or improve the feature, to comply with applicable law, or as part of a merger or acquisition with notice.</li>
                    <li>We do not use Google user data for advertising, including personalized or retargeted advertising.</li>
                    <li>We do not allow humans to read Google user data unless the user affirmatively agrees, it is necessary for security or legal compliance, or the data is aggregated and anonymized.</li>
                    <li>We do not use Google user data for AI model training or to generate content, including non-consensual intimate imagery (NCI).</li>
                  </ul>
                </section>

                <section lang="zh-CN">
                  <h2>中文摘要</h2>
                  <p>本服务仅使用 Google 账号的邮箱地址和 Gmail 发信权限发送事务性通知。Refresh Token 使用 Data Protection 加密保存，不写入日志、不出售、不用于广告或 AI 训练。用户可在 Google 账号权限页撤销授权，或通过上方邮箱联系我们删除数据。</p>
                </section>
                """;

            return Page("Privacy Policy", $"How {options.PublicAppName} accesses, uses, stores and deletes Google user data.", body);
        }

        [HttpGet("terms")]
        public IActionResult Terms()
        {
            var appName = H(options.PublicAppName);
            var contact = H(options.PublicContactEmail);

            var body = $$"""
                <section class="hero">
                  <p class="eyebrow">Terms of Service</p>
                  <h1>Terms of Service</h1>
                  <p class="lead">Terms for using the {{appName}} email notification component.</p>
                  <p class="meta">Effective date: {{LastUpdated}} &middot; Last updated: {{LastUpdated}}</p>
                </section>

                <section>
                  <h2>1. Acceptance</h2>
                  <p>By using {{appName}} ("the Service"), you agree to these terms. If you do not agree, do not use the Service. The Service is operated by the administrator of a self-hosted CustomFumenProviderWebServer instance.</p>
                </section>

                <section>
                  <h2>2. The Service</h2>
                  <p>The Service is a self-hosted email notification component. It uses Google OAuth and the Gmail API to send transactional notification emails related to custom rhythm-game chart submissions, review results, publication status changes, and administrative test messages.</p>
                </section>

                <section>
                  <h2>3. Google API use</h2>
                  <p>The Service uses the Gmail API only to send emails requested by the administrator. It does not read, modify, or delete Gmail messages. You must comply with the <a href="https://developers.google.com/terms">Google APIs Terms of Service</a> and the <a href="https://developers.google.com/terms/api-services-user-data-policy">Google API Services User Data Policy</a>. You may not use the Service in a way that violates Google's terms or policies.</p>
                </section>

                <section>
                  <h2>4. Authorized use</h2>
                  <ul>
                    <li>You must be the administrator or an authorized operator of the self-hosted instance.</li>
                    <li>You must have permission to send email to the recipients you select.</li>
                    <li>You must not send spam, unsolicited bulk email, or deceptive messages.</li>
                    <li>You must not send unlawful, harassing, abusive, or malicious content.</li>
                    <li>You must not use the Service to create or distribute AI-generated non-consensual intimate imagery (NCI) or any other prohibited content.</li>
                    <li>You must not use the Service to violate applicable law or the rights of others.</li>
                  </ul>
                </section>

                <section>
                  <h2>5. Data and privacy</h2>
                  <p>The <a href="/oauth/google/public/privacy">Privacy Policy</a> explains how the Service accesses, uses, stores, and deletes Google user data. You are responsible for the content you send and for choosing recipients and email content lawfully.</p>
                </section>

                <section>
                  <h2>6. Limited Use</h2>
                  <p>The Service's use of Google user data is limited to providing the email notification feature. We do not sell Google user data, use it for advertising, or use it to train AI models. We do not transfer it to third parties except as necessary to provide the feature, comply with law, or as part of a merger or acquisition with notice.</p>
                </section>

                <section>
                  <h2>7. Availability and warranty</h2>
                  <p>The Service is provided "as is" and "as available", without warranties of any kind, express or implied, including fitness for a particular purpose, merchantability, or non-infringement. The Service may be unavailable due to maintenance, network failures, Google API changes, or other causes.</p>
                </section>

                <section>
                  <h2>8. Limitation of liability</h2>
                  <p>To the maximum extent permitted by applicable law, the operator of the Service is not liable for indirect, incidental, special, consequential, or punitive damages, or for lost data, lost profits, or business interruption arising from the use of or inability to use the Service.</p>
                </section>

                <section>
                  <h2>9. Termination</h2>
                  <p>Access may be suspended or terminated if these terms or Google's policies are violated, or if the Service is discontinued. The administrator may revoke the Google authorization at any time through the Google Account permissions page.</p>
                </section>

                <section>
                  <h2>10. Changes</h2>
                  <p>We may update these terms from time to time. The effective date at the top of this page will change when we do. Continued use of the Service after changes means you accept the updated terms.</p>
                </section>

                <section>
                  <h2>11. Contact</h2>
                  <p>Questions about these terms: <a href="mailto:{{contact}}">{{contact}}</a>.</p>
                </section>

                <section lang="zh-CN">
                  <h2>中文摘要</h2>
                  <p>本服务仅用于发送谱面审核、发布状态等事务性通知。禁止用于垃圾邮件、骚扰、违法内容或 AI 生成的未经同意的私密影像（NCI）。使用 Gmail API 时需遵守 Google 的相关条款与用户数据政策。</p>
                </section>
                """;

            return Page("Terms of Service", $"Terms of service for the {options.PublicAppName} Gmail notification component.", body);
        }

        private ContentResult Page(string title, string description, string body)
        {
            var appName = H(options.PublicAppName);
            var contact = H(options.PublicContactEmail);

            var html = $$"""
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1">
                  <meta name="description" content="{{H(description)}}">
                  <meta name="robots" content="index, follow">
                  <title>{{H(title)}} &middot; {{appName}}</title>
                  <style>
                    :root {
                      color-scheme: light dark;
                      --bg: #f6f7f9;
                      --surface: #ffffff;
                      --fg: #20242a;
                      --muted: #5f6875;
                      --border: #dfe3e8;
                      --link: #0b63ce;
                      --accent: #0b63ce;
                      --notice: #eef5ff;
                      --code: #f1f3f5;
                    }
                    @media (prefers-color-scheme: dark) {
                      :root {
                        --bg: #14171c;
                        --surface: #1b1f26;
                        --fg: #e8eaed;
                        --muted: #a8b0bb;
                        --border: #303743;
                        --link: #7fb3ff;
                        --accent: #7fb3ff;
                        --notice: #172436;
                        --code: #252b34;
                      }
                    }
                    * { box-sizing: border-box; }
                    html { scroll-behavior: smooth; }
                    body {
                      margin: 0;
                      background: var(--bg);
                      color: var(--fg);
                      font-family: ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "PingFang SC", "Microsoft YaHei", sans-serif;
                      line-height: 1.65;
                    }
                    a { color: var(--link); text-underline-offset: 2px; }
                    a:hover { text-decoration-thickness: 2px; }
                    .wrap { width: min(920px, calc(100% - 2rem)); margin: 0 auto; }
                    .site-header {
                      position: sticky;
                      top: 0;
                      z-index: 10;
                      background: color-mix(in srgb, var(--surface) 92%, transparent);
                      border-bottom: 1px solid var(--border);
                      backdrop-filter: blur(8px);
                    }
                    .site-header .wrap {
                      display: flex;
                      align-items: center;
                      justify-content: space-between;
                      gap: 1rem;
                      padding: 0.85rem 0;
                      flex-wrap: wrap;
                    }
                    .brand { font-weight: 700; color: var(--fg); text-decoration: none; letter-spacing: 0.01em; }
                    nav { display: flex; gap: 0.35rem; flex-wrap: wrap; }
                    nav a {
                      color: var(--muted);
                      text-decoration: none;
                      font-size: 0.92rem;
                      padding: 0.35rem 0.6rem;
                      border-radius: 0.45rem;
                    }
                    nav a:hover { background: var(--code); color: var(--fg); }
                    main { padding: 2.5rem 0 1rem; }
                    section {
                      background: var(--surface);
                      border: 1px solid var(--border);
                      border-radius: 0.8rem;
                      padding: 1.25rem 1.4rem;
                      margin: 0 0 1rem;
                    }
                    section.hero { padding: 2rem 1.6rem; }
                    .eyebrow {
                      margin: 0 0 0.35rem;
                      color: var(--accent);
                      font-size: 0.78rem;
                      font-weight: 700;
                      letter-spacing: 0.08em;
                      text-transform: uppercase;
                    }
                    h1 { margin: 0 0 0.6rem; font-size: clamp(1.7rem, 4vw, 2.35rem); line-height: 1.2; }
                    h2 { margin: 0 0 0.7rem; font-size: 1.2rem; line-height: 1.3; }
                    p { margin: 0 0 0.8rem; }
                    p:last-child, ul:last-child, ol:last-child { margin-bottom: 0; }
                    .lead { color: var(--muted); font-size: 1.08rem; max-width: 70ch; }
                    .meta { color: var(--muted); font-size: 0.9rem; }
                    .notice {
                      background: var(--notice);
                      border: 1px solid color-mix(in srgb, var(--accent) 30%, var(--border));
                      border-radius: 0.6rem;
                      padding: 0.75rem 0.9rem;
                      color: var(--fg);
                      font-size: 0.95rem;
                      margin-top: 1rem;
                    }
                    ul, ol { padding-left: 1.3rem; }
                    li { margin: 0.3rem 0; }
                    .table-wrap { overflow-x: auto; margin: 0.5rem 0 0.9rem; }
                    table { width: 100%; border-collapse: collapse; font-size: 0.95rem; }
                    th, td { text-align: left; vertical-align: top; padding: 0.6rem 0.7rem; border-bottom: 1px solid var(--border); }
                    th { color: var(--muted); font-weight: 600; white-space: nowrap; }
                    tbody tr:last-child td, tbody tr:last-child th { border-bottom: 0; }
                    code {
                      background: var(--code);
                      border: 1px solid var(--border);
                      border-radius: 0.35rem;
                      padding: 0.08rem 0.35rem;
                      font-size: 0.88em;
                      word-break: break-word;
                    }
                    .documents { list-style: disc; }
                    .steps li { margin: 0.45rem 0; }
                    .site-footer {
                      border-top: 1px solid var(--border);
                      margin-top: 1rem;
                      padding: 1.5rem 0 2.5rem;
                      color: var(--muted);
                      font-size: 0.9rem;
                    }
                    .site-footer p { margin: 0.2rem 0; }
                    @media (max-width: 640px) {
                      section { padding: 1rem; }
                      section.hero { padding: 1.4rem 1rem; }
                      .site-header .wrap { align-items: flex-start; flex-direction: column; }
                    }
                  </style>
                </head>
                <body>
                  <header class="site-header">
                    <div class="wrap">
                      <a class="brand" href="/oauth/google/public/">{{appName}}</a>
                      <nav aria-label="Public information">
                        <a href="/oauth/google/public/">Home</a>
                        <a href="/oauth/google/public/purpose">App purpose</a>
                        <a href="/oauth/google/public/privacy">Privacy</a>
                        <a href="/oauth/google/public/terms">Terms</a>
                      </nav>
                    </div>
                  </header>
                  <main class="wrap">
                    {{body}}
                  </main>
                  <footer class="site-footer">
                    <div class="wrap">
                      <p>{{appName}} is a self-hosted component of the CustomFumenProviderWebServer project.</p>
                      <p>Contact: <a href="mailto:{{contact}}">{{contact}}</a></p>
                      <p>Last updated: {{LastUpdated}}</p>
                    </div>
                  </footer>
                </body>
                </html>
                """;

            if (ControllerContext?.HttpContext is { } httpContext)
            {
                httpContext.Response.Headers["Content-Security-Policy"] =
                    "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
                httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
                httpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
                httpContext.Response.Headers["Cache-Control"] = "public, max-age=600";
            }

            return new ContentResult
            {
                StatusCode = StatusCodes.Status200OK,
                ContentType = "text/html; charset=utf-8",
                Content = html,
            };
        }

        private static string H(string? value)
        {
            return HtmlEncoder.Default.Encode(value ?? string.Empty);
        }
    }
}
