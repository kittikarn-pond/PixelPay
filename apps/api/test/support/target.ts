import { Test } from '@nestjs/testing';
import { ValidationPipe } from '@nestjs/common';
import { FastifyAdapter, NestFastifyApplication } from '@nestjs/platform-fastify';
import { PrismaClient } from '@prisma/client';
import type { Server } from 'http';
import { AppModule } from '../../src/app.module';
import { PrismaService } from '../../src/prisma/prisma.service';
import { ResponseInterceptor } from '../../src/common/interceptors/response.interceptor';
import { HttpExceptionFilter } from '../../src/common/filters/http-exception.filter';

export interface Target {
  http: string | Server;
  prisma: PrismaClient;
  close(): Promise<void>;
}

export async function createTarget(): Promise<Target> {
  const baseUrl = process.env.E2E_BASE_URL;
  if (baseUrl) {
    const prisma = new PrismaClient();
    return { http: baseUrl, prisma, close: () => prisma.$disconnect() };
  }

  const module = await Test.createTestingModule({ imports: [AppModule] }).compile();
  const app = module.createNestApplication<NestFastifyApplication>(new FastifyAdapter());
  app.setGlobalPrefix('api/v1');
  app.useGlobalPipes(new ValidationPipe({ whitelist: true, transform: true }));
  app.useGlobalInterceptors(new ResponseInterceptor());
  app.useGlobalFilters(new HttpExceptionFilter());
  await app.init();
  await app.getHttpAdapter().getInstance().ready();
  return {
    http: app.getHttpServer(),
    prisma: app.get(PrismaService),
    close: () => app.close(),
  };
}
