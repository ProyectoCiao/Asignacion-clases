import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-placeholder-page',
  standalone: true,
  templateUrl: './placeholder-page.html',
  styleUrl: './placeholder-page.css',
})
export class PlaceholderPageComponent {
  private readonly route = inject(ActivatedRoute);

  protected readonly titulo = this.route.snapshot.data['titulo'] ?? 'Pantalla';
  protected readonly descripcion = this.route.snapshot.data['descripcion'] ?? '';
}
